using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace USAC
{
    // 合同生命周期管理
    public class DebtContractManager : IExposable
    {
        #region 字段
        public List<DebtContract> ActiveContracts = new();
        private DebtScheduler scheduler = new();
        #endregion

        #region 属性
        // 总负债
        public float TotalDebt
        {
            get
            {
                float sum = 0f;
                for (int i = 0; i < ActiveContracts.Count; i++)
                {
                    var c = ActiveContracts[i];
                    if (c.IsActive) sum += c.Principal + c.AccruedInterest;
                }
                return sum;
            }
        }

        // 最近到期的合同 排除据点模式
        public DebtContract NextDueContract
        {
            get
            {
                DebtContract best = null;
                for (int i = 0; i < ActiveContracts.Count; i++)
                {
                    var c = ActiveContracts[i];
                    if (c.IsActive && !c.IsInSiteMode && (best == null || c.NextCycleTick < best.NextCycleTick))
                        best = c;
                }
                return best;
            }
        }

        // 活跃合同数量
        public int ActiveCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < ActiveContracts.Count; i++)
                    if (ActiveContracts[i].IsActive) count++;
                return count;
            }
        }
        #endregion

        #region 合同管理
        // 申请贷款
        public void ApplyForLoan(DebtType type, float amount,
            float growthRate, float interestRate,
            DebtGrowthMode growthMode = DebtGrowthMode.PrincipalBased)
        {
            var contract = new DebtContract(
                type, amount, growthRate, interestRate, growthMode);

            ActiveContracts.Add(contract);

            // 注册到调度器
            scheduler.ScheduleContractCycle(contract, () => ProcessContractCycle(contract));

            // 投放债券
            Map map = GameComponent_USACDebt.GetRichestPlayerHomeMap();
            if (map != null)
            {
                int bondCount = Mathf.FloorToInt(amount / 1000f);
                if (bondCount > 0)
                {
                    Thing bonds = ThingMaker.MakeThing(USAC_DefOf.USAC_Bond);
                    bonds.stackCount = bondCount;
                    DropPodUtility.DropThingsNear(
                        DropCellFinder.TradeDropSpot(map),
                        map, new[] { bonds });
                }
            }
        }

        // 增加债务本金
        public void AddDebt(float amount, string reason)
        {
            if (ActiveContracts.Count == 0)
            {
                ApplyForLoan(DebtType.DynamicLoan, amount, 0.05f, 0.02f);
                return;
            }
            DebtHandler.AdjustPrincipal(ActiveContracts[0], amount, reason, USACTransactionType.Initial);
        }
        #endregion

        #region 周期处理
        public void Tick()
        {
            if (ActiveContracts == null || ActiveContracts.Count == 0)
                return;

            int now = Find.TickManager.TicksGame;

            // 调度器检查并触发到期事件
            scheduler.CheckAndTrigger(now);

            // 清理已结清的合同
            for (int i = ActiveContracts.Count - 1; i >= 0; i--)
            {
                var contract = ActiveContracts[i];
                if (contract.IsActive && contract.Principal <= 0)
                {
                    contract.IsActive = false;
                    scheduler.UnscheduleContract(contract.ContractId);

                    // 发布合同结清事件
                    DebtEventBus.Instance.Publish(new DebtEventArgs
                    {
                        EventType = DebtEventType.ContractSettled,
                        Contract = contract,
                        Amount = 0,
                        Reason = "Contract settled"
                    });
                }
            }
        }

        internal void ProcessContractCycle(DebtContract contract)
        {
            // 合同已结清则立即退出
            if (!contract.IsActive || contract.Principal <= 0)
            {
                contract.IsActive = false;
                scheduler.UnscheduleContract(contract.ContractId);
                return;
            }

            Map map = GameComponent_USACDebt.GetRichestPlayerHomeMap();
            int now = Find.TickManager.TicksGame;

            // 检查周期时间异常
            if (contract.NextCycleTick <= 0)
            {
                contract.NextCycleTick = now + DebtContract.CycleTicks;
                scheduler.ScheduleContractCycle(contract, () => ProcessContractCycle(contract));
                Log.Warning($"[USAC] 合同 {contract.Label} NextCycleTick异常 已重置");
                return;
            }

            // 计算错过的周期数
            int missedCycles = 0;
            int nextTick = contract.NextCycleTick;

            while (nextTick <= now && missedCycles < 10)
            {
                missedCycles++;
                nextTick += DebtContract.CycleTicks;
            }

            // 如果没有错过周期 说明被提前触发 重新调度
            if (missedCycles == 0)
            {
                scheduler.ScheduleContractCycle(contract, () => ProcessContractCycle(contract));
                return;
            }

            // 批量处理错过的周期
            for (int i = 0; i < missedCycles; i++)
            {
                contract.ProcessCycle(map);

                // 据点模式或非最后一个周期 自动将利息并入本金
                if (contract.IsInSiteMode || i < missedCycles - 1)
                {
                    if (contract.AccruedInterest > 0)
                    {
                        float interest = contract.AccruedInterest;
                        DebtHandler.AdjustPrincipal(contract, interest,
                            "USAC.Debt.Transaction.MissedPayment".Translate(contract.Label, contract.MissedPayments),
                            USACTransactionType.Penalty);
                        DebtHandler.SetAccruedInterest(contract, 0f);
                    }
                }
            }

            // 最后一个周期如果不是据点模式 弹窗
            if (!contract.IsInSiteMode)
            {
                ShowRepaymentDialog(contract, map);
            }

            // 推进到下一个周期
            contract.NextCycleTick = nextTick;

            // 重新调度下次周期
            scheduler.ScheduleContractCycle(contract, () => ProcessContractCycle(contract));

            if (missedCycles > 1)
            {
                Log.Warning($"[USAC] 合同 {contract.Label} 错过了 {missedCycles} 个周期 已批量处理");
            }
        }

        private void ShowRepaymentDialog(DebtContract contract, Map map)
        {
            float toPay = contract.AccruedInterest;
            int bondsNeeded = Mathf.CeilToInt(toPay / 1000f);

            string text =
                "USAC.Debt.Dialog.Repayment.Text".Translate(
                    contract.Label,
                    toPay.ToString("N0"),
                    bondsNeeded,
                    contract.Principal.ToString("N0"),
                    contract.MissedPayments);
            if (contract.MissedPayments >= 2) text += "USAC.Debt.Dialog.Repayment.Warning".Translate();

            DiaNode diaNode = new DiaNode(text);

            // 确认缴纳
            DiaOption optPay = new DiaOption(
                "USAC.Debt.Dialog.Repayment.Option.Pay".Translate())
            {
                action = () =>
                {
                    if (contract.TryPayInterest(map))
                    {
                        Messages.Message(
                            "USAC.Debt.Message.InterestPaid".Translate(contract.Label),
                            MessageTypeDefOf.PositiveEvent);
                    }
                    else
                    {
                        // 通知债务组件处理失败还款
                        var debtComp = GameComponent_USACDebt.Instance;
                        if (debtComp != null)
                        {
                            debtComp.HandleFailedPayment(contract, map);
                        }
                    }
                },
                resolveTree = true
            };

            // 拒绝/无力偿还
            DiaOption optRefuse = new DiaOption(
                "USAC.Debt.Dialog.Repayment.Option.Refuse".Translate())
            {
                action = () =>
                {
                    var debtComp = GameComponent_USACDebt.Instance;
                    if (debtComp != null)
                    {
                        debtComp.HandleFailedPayment(contract, map);
                    }
                },
                resolveTree = true
            };

            diaNode.options.Add(optPay);
            diaNode.options.Add(optRefuse);

            Find.WindowStack.Add(new Dialog_NodeTree(
                diaNode, true, false,
                "USAC.Debt.Dialog.Repayment.Title".Translate(contract.Label)));
        }
        #endregion

        #region 加载与存档
        public void LoadedGame(GameComponent_USACDebt debtComp)
        {
            if (ActiveContracts == null)
                ActiveContracts = new List<DebtContract>();

            // 重建调度器回调
            scheduler.RebuildCallbacks(debtComp);

            // 尝试修复旧版卡死存档的孤儿合同
            RepairOrphanedSchedules();
        }

        private void RepairOrphanedSchedules()
        {
            int now = Find.TickManager.TicksGame;
            for (int i = 0; i < ActiveContracts.Count; i++)
            {
                var c = ActiveContracts[i];
                if (!c.IsActive) continue;

                // NextCycleTick损坏修复
                if (c.NextCycleTick <= 0)
                {
                    c.NextCycleTick = now + 1;
                    Log.Warning($"[USAC] 合同 {c.Label}({c.ContractId}) NextCycleTick损坏 已修复为立即触发");
                }

                // 补注缺失的调度事件
                if (!scheduler.IsContractScheduled(c.ContractId))
                {
                    var contract = c;
                    scheduler.ScheduleContractCycle(contract, () => ProcessContractCycle(contract));
                    Log.Warning($"[USAC] 合同 {c.Label}({c.ContractId}) 调度事件缺失 已自动补注");
                }
            }
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref ActiveContracts, "ActiveContracts", LookMode.Deep);
            Scribe_Deep.Look(ref scheduler, "scheduler");

            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                if (ActiveContracts == null)
                    ActiveContracts = new List<DebtContract>();
                if (scheduler == null)
                    scheduler = new DebtScheduler();
            }
        }
        #endregion
    }
}
