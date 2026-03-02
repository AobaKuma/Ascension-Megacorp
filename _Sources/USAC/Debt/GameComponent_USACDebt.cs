using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace USAC
{
    // 贷款管理组件
    public class GameComponent_USACDebt : GameComponent
    {
        #region 字段
        public int CreditScore = 50;
        public List<DebtContract> ActiveContracts = new();
        public List<USACDebtTransaction> Transactions = new();

        // 旧版兼容迁移字段
        private float legacyTotalDebt;
        private float legacyInterest;
        #endregion

        public GameComponent_USACDebt(Game game) { }

        #region 属性
        // 总负债
        public float TotalDebt =>
            ActiveContracts
                .Where(c => c.IsActive)
                .Sum(c => c.Principal + c.AccruedInterest);

        // 最近到期的合同
        public DebtContract NextDueContract =>
            ActiveContracts
                .Where(c => c.IsActive)
                .OrderBy(c => c.NextCycleTick)
                .FirstOrDefault();

        // 活跃合同数量
        public int ActiveCount =>
            ActiveContracts.Count(c => c.IsActive);

        // 增加债务本金（服务费/租赁费）
        public void AddDebt(float amount, string reason)
        {
            if (ActiveContracts.Count == 0)
            {
                ApplyForLoan(DebtType.DynamicLoan, amount, 0.05f, 0.02f);
                return;
            }
            ActiveContracts[0].Principal += amount;
            AddTransaction(USACTransactionType.Initial, amount, reason);
        }

        public static GameComponent_USACDebt Instance =>
            Current.Game?.GetComponent<GameComponent_USACDebt>();
        #endregion

        #region 生命周期
        public override void StartedNewGame()
        {
            if (ActiveContracts == null)
                ActiveContracts = new List<DebtContract>();
        }

        public override void LoadedGame()
        {
            if (ActiveContracts == null)
                ActiveContracts = new List<DebtContract>();

            MigrateLegacyData();
        }

        public override void GameComponentTick()
        {
            if (ActiveContracts == null || ActiveContracts.Count == 0)
                return;
            // 每2000tick检查一次
            if (Find.TickManager.TicksGame % 2000 != 0) return;

            int now = Find.TickManager.TicksGame;

            foreach (var contract in ActiveContracts)
            {
                if (!contract.IsActive) continue;
                if (contract.Principal <= 0)
                {
                    contract.IsActive = false;
                    continue;
                }

                if (now >= contract.NextCycleTick)
                {
                    ProcessContractCycle(contract);
                }
            }

            // 清理已结清的合同
            ActiveContracts.RemoveAll(
                c => !c.IsActive && c.Principal <= 0);
        }
        #endregion

        #region 周期结算
        private void ProcessContractCycle(DebtContract contract)
        {
            Map map = Find.AnyPlayerHomeMap;

            // 执行周期结算
            contract.ProcessCycle(map);

            // 弹窗询问还款
            ShowRepaymentDialog(contract, map);

            // 重置周期
            contract.NextCycleTick =
                Find.TickManager.TicksGame + DebtContract.CycleTicks;
        }

        private void ShowRepaymentDialog(
            DebtContract contract, Map map)
        {
            float toPay = contract.AccruedInterest;
            int bondsNeeded = Mathf.CeilToInt(toPay / 1000f);

            string text =
                $"[USAC 金融节点] {contract.Label}审计完成\n\n" +
                $"本期应付利息: ₿{toPay:N0}" +
                $" (需{bondsNeeded}张债券)\n" +
                $"当前合同本金: ₿{contract.Principal:N0}\n" +
                $"累计欠缴: {contract.MissedPayments}次\n\n" +
                "请确认是否使用贸易信标范围内的" +
                "\"USAC信用债券\"进行核销。\n" +
                (contract.MissedPayments >= 2
                    ? "⚠ 再次欠缴将触发强制征收程序！"
                    : "");

            DiaNode diaNode = new DiaNode(text);

            // 确认缴纳
            DiaOption optPay = new DiaOption("执行核销（使用债券）")
            {
                action = () =>
                {
                    if (contract.TryPayInterest(map))
                    {
                        Messages.Message(
                            $"[USAC] {contract.Label}" +
                            " 利息缴纳完成",
                            MessageTypeDefOf.PositiveEvent);
                    }
                    else
                    {
                        HandleFailedPayment(contract, map);
                    }
                },
                resolveTree = true
            };

            // 拒绝/无力偿还
            DiaOption optRefuse = new DiaOption("无力偿还/拒绝")
            {
                action = () =>
                {
                    HandleFailedPayment(contract, map);
                },
                resolveTree = true
            };

            diaNode.options.Add(optPay);
            diaNode.options.Add(optRefuse);

            Find.WindowStack.Add(new Dialog_NodeTree(
                diaNode, true, false,
                $"USAC {contract.Label}结算"));
        }

        private void HandleFailedPayment(
            DebtContract contract, Map map)
        {
            CreditScore = Mathf.Max(0, CreditScore - 15);
            contract.HandleMissedPayment();

            // 第3次欠缴即触发强制征收
            if (contract.ShouldForceCollect)
            {
                ForceCollect(contract, map);
            }
        }
        #endregion

        #region 强制征收
        public void ForceCollect(DebtContract contract, Map map)
        {
            if (map == null) return;

            // 检测地图是否对轨道载具完全封闭
            if (IsMapSealedFromOrbit(map))
            {
                Messages.Message(
                    "[USAC] 目标殖民地处于轨道屏蔽区域，强制征收程序已暂缓。",
                    MessageTypeDefOf.NeutralEvent);
                return;
            }

            var strategy = CollectionStrategyFactory
                .Create(contract.Type);
            float targetAmount = contract.AccruedInterest > 0
                ? contract.AccruedInterest
                : contract.Principal * 0.1f;

            float collected = strategy.Execute(
                map, targetAmount, contract);

            contract.Principal = Mathf.Max(0,
                contract.Principal - collected);

            AddTransaction(USACTransactionType.Penalty,
                collected,
                $"{contract.Label} 强制征收 (第" +
                $"{contract.MissedPayments}次)");
        }

        // 检查地图屏蔽状态
        private static bool IsMapSealedFromOrbit(Map map)
        {
            // 判定全屋顶覆盖
            var roofGrid = map.roofGrid;
            int total = map.cellIndices.NumGridCells;
            for (int i = 0; i < total; i++)
            {
                if (roofGrid.RoofAt(i) == null)
                    return false;
            }

            // 判定四周边界封闭
            int w = map.Size.x;
            int h = map.Size.z;
            for (int x = 0; x < w; x++)
            {
                if (new IntVec3(x, 0, 0).Walkable(map)) return false;
                if (new IntVec3(x, 0, h - 1).Walkable(map)) return false;
            }
            for (int z = 1; z < h - 1; z++)
            {
                if (new IntVec3(0, 0, z).Walkable(map)) return false;
                if (new IntVec3(w - 1, 0, z).Walkable(map)) return false;
            }

            return true;
        }
        #endregion

        #region 贷款申请
        public void ApplyForLoan(DebtType type, float amount,
            float growthRate, float interestRate,
            DebtGrowthMode growthMode = DebtGrowthMode.PrincipalBased)
        {
            float debtAmount = amount;

            var contract = new DebtContract(
                type, debtAmount, growthRate, interestRate, growthMode);

            ActiveContracts.Add(contract);
            CreditScore = Mathf.Max(0, CreditScore - 2);

            // 投放债券
            Map map = Find.AnyPlayerHomeMap;
            if (map != null)
            {
                int bondCount = Mathf.FloorToInt(amount / 1000f);
                if (bondCount > 0)
                {
                    Thing bonds = ThingMaker.MakeThing(
                        USAC_DefOf.USAC_Bond);
                    bonds.stackCount = bondCount;
                    DropPodUtility.DropThingsNear(
                        DropCellFinder.TradeDropSpot(map),
                        map, new[] { bonds });
                }
            }

            AddTransaction(USACTransactionType.Initial,
                debtAmount,
                $"签署{contract.Label}合同");
        }

        // 贷款风险定价评估
        public UnifiedLoanEval EvaluateLoan(float interestRate, float growthRate, DebtGrowthMode growthMode)
        {
            float wealth = Find.AnyPlayerHomeMap?.wealthWatcher?.WealthTotal ?? 0f;
            float totalDebt = TotalDebt;

            // 基础信用系数
            float baseCreditFactor = Mathf.Lerp(0.1f, 0.3f, (CreditScore - 30f) / 70f);
            if (CreditScore < 30) baseCreditFactor = 0f;

            // 利率参数加成
            float interestBonus = interestRate * 1.5f;

            // 风险参数加成
            float growthBonus = growthRate * 2.5f;

            // 结算综合倍率
            float totalMult = baseCreditFactor + interestBonus + growthBonus;

            // 环境信用折扣
            float creditDiscount = Mathf.Clamp01((CreditScore - 30) / 175f) * 0.30f;
            float actualInterest = Mathf.Round(interestRate * (1f - creditDiscount) * 1000f) / 1000f;

            // 计算可用额度
            float rawMax = wealth * totalMult - totalDebt;
            float maxAmount = Mathf.Floor(Mathf.Max(0f, rawMax) / 1000f) * 1000f;

            string blockReason = null;
            if (CreditScore < 30)
                blockReason = "USAC.UI.Assets.Block.LowCredit".Translate();
            else if (maxAmount < 1000f)
                blockReason = "USAC.UI.Assets.Block.LowWealth".Translate();

            return new UnifiedLoanEval
            {
                MaxAmount = maxAmount,
                InterestRate = actualInterest,
                GrowthRate = growthRate,
                GrowthMode = growthMode,
                Wealth = wealth,
                CreditDiscount = creditDiscount,
                IsAvailable = blockReason == null,
                BlockReason = blockReason
            };
        }

        // 返回距下次结算的可读时间字符串
        public static string GetTimeToNextCycle(DebtContract c)
        {
            int ticks = c.NextCycleTick - Find.TickManager.TicksGame;
            if (ticks <= 0) return "USAC.UI.Assets.Imminent".Translate();
            return GenDate.ToStringTicksToPeriod(ticks, false);
        }

        // 预测下一周期本金增长量
        public static float PredictNextGrowth(DebtContract c)
        {
            if (c.GrowthRate <= 0f) return 0f;
            if (c.GrowthMode == DebtGrowthMode.WealthBased)
                return (Find.AnyPlayerHomeMap?.wealthWatcher?.WealthTotal ?? 0f) * c.GrowthRate;
            return c.Principal * c.GrowthRate;
        }
        #endregion

        #region 债券操作(公开给合同使用)
        public int GetBondCountNearBeacons(Map map)
        {
            if (map == null) return 0;
            int count = 0;
            var beacons = map.listerBuildings
                .allBuildingsColonist
                .Where(b => b is Building_OrbitalTradeBeacon);

            foreach (var b in beacons)
            {
                var beacon = (Building_OrbitalTradeBeacon)b;
                foreach (IntVec3 c in beacon.TradeableCells)
                {
                    var bond = c.GetFirstThing(
                        map, USAC_DefOf.USAC_Bond);
                    if (bond != null)
                        count += bond.stackCount;
                }
            }
            return count;
        }

        public void ConsumeBondsNearBeacons(Map map, int count)
        {
            int remaining = count;
            var beacons = map.listerBuildings
                .allBuildingsColonist
                .Where(b => b is Building_OrbitalTradeBeacon);

            foreach (var b in beacons)
            {
                var beacon = (Building_OrbitalTradeBeacon)b;
                foreach (IntVec3 c in beacon.TradeableCells)
                {
                    var bond = c.GetFirstThing(
                        map, USAC_DefOf.USAC_Bond);
                    if (bond == null) continue;

                    int take = Math.Min(remaining, bond.stackCount);
                    bond.SplitOff(take).Destroy();
                    remaining -= take;
                    if (remaining <= 0) return;
                }
            }
        }

        public int GetBondCountOnMap()
        {
            Map map = Find.AnyPlayerHomeMap;
            return map?.listerThings
                .ThingsOfDef(USAC_DefOf.USAC_Bond)
                .Sum(b => b.stackCount) ?? 0;
        }

        public void ConsumeBonds(Map map, int count)
        {
            int remaining = count;
            foreach (var b in map.listerThings
                .ThingsOfDef(USAC_DefOf.USAC_Bond))
            {
                int take = Math.Min(remaining, b.stackCount);
                b.SplitOff(take).Destroy();
                remaining -= take;
                if (remaining <= 0) break;
            }
        }
        #endregion

        #region 交易记录
        public void AddTransaction(USACTransactionType type,
            float amount, string note)
        {
            Transactions.Insert(0, new USACDebtTransaction
            {
                Type = type,
                Amount = amount,
                Note = note,
                TicksGame = Find.TickManager.TicksGame
            });
            if (Transactions.Count > 50)
                Transactions.RemoveAt(Transactions.Count - 1);
        }
        #endregion

        #region 调试接口
        // 开发者接口
        public void Debug_SkipCycle()
        {
            if (ActiveContracts == null) return;
            foreach (var contract in ActiveContracts.ToList())
            {
                if (contract.IsActive)
                {
                    ProcessContractCycle(contract);
                }
            }
        }
        #endregion

        #region 存档
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref CreditScore, "CreditScore", 50);

            // 旧版兼容读取
            Scribe_Values.Look(ref legacyTotalDebt, "TotalDebt");
            Scribe_Values.Look(ref legacyInterest,
                "TotalInterestAccrued");

            Scribe_Collections.Look(ref ActiveContracts,
                "ActiveContracts", LookMode.Deep);
            Scribe_Collections.Look(ref Transactions,
                "Transactions", LookMode.Deep);
        }

        // 旧存档迁移
        private void MigrateLegacyData()
        {
            if (legacyTotalDebt <= 0) return;
            if (ActiveContracts.Any(c => c.IsActive)) return;

            var legacy = new DebtContract(
                DebtType.WholeMortgage,
                legacyTotalDebt, 0.20f, 0.05f,
                DebtGrowthMode.WealthBased);
            legacy.AccruedInterest = legacyInterest;

            ActiveContracts.Add(legacy);
            legacyTotalDebt = 0;
            legacyInterest = 0;

            Log.Message("[USAC] 已迁移旧版债务数据至新合同系统");
        }
        #endregion
    }
}
