using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace USAC
{
    // 债务收缴协调器
    public class DebtCollectionCoordinator : IExposable
    {
        #region 字段
        // 当前收缴轮次是否有夹具被摧毁
        public bool HasGripperDestroyedThisRound;
        #endregion

        #region 存档
        public void ExposeData()
        {
            Scribe_Values.Look(ref HasGripperDestroyedThisRound, "HasGripperDestroyedThisRound", false);
        }
        #endregion

        #region 强制征收
        public void ForceCollect(DebtContract contract, Map map)
        {
            if (map == null) return;

            // 重置本轮收缴状态
            HasGripperDestroyedThisRound = false;

            // 检测地图封闭状态
            if (IsMapSealedFromOrbit(map))
            {
                Messages.Message(
                    "USAC.Debt.Message.ForceCollectPausedByShield".Translate(),
                    MessageTypeDefOf.NeutralEvent);

                HandleCollectionFailure(contract, map);
                return;
            }

            var strategy = CollectionStrategyFactory.Create(contract.Type);
            float targetAmount = contract.AccruedInterest > 0
                ? contract.AccruedInterest
                : contract.Principal * 0.1f;

            // 派遣夹具（异步过程）
            strategy.Execute(map, targetAmount, contract);
        }

        public void HandleCollectionFailure(DebtContract contract, Map map)
        {
            contract.ConsecutiveCollectionFails++;

            var debtComp = GameComponent_USACDebt.Instance;
            if (debtComp == null) return;

            // 第1轮抗缴警告锁定
            if (contract.ConsecutiveCollectionFails == 1)
            {
                debtComp.IsSystemLocked = true;
                debtComp.CreditScore = 0;
                Find.LetterStack.ReceiveLetter(
                    "USAC_DebtSite_WarningLetterLabel".Translate(),
                    "USAC_DebtSite_WarningLetterText".Translate(contract.Label),
                    LetterDefOf.NegativeEvent);
            }
            // 抗缴升级据点模式并弹窗
            else if (contract.ConsecutiveCollectionFails == 2)
            {
                Log.Message($"[USAC] 合同 {contract.Label} 连续抗缴2次 升级为据点模式");

                // 标记所有活跃合同进入据点模式
                foreach (var c in debtComp.ActiveContracts)
                {
                    if (c.IsActive)
                    {
                        c.IsInSiteMode = true;
                        Log.Message($"[USAC] 合同 {c.Label} 进入据点模式");
                    }
                }

                // 初始化据点生成绝对触发时刻
                debtComp.TicksUntilNextSiteBatch = 900000;
                Log.Message($"[USAC] 设置据点生成计时器 当前tick={Find.TickManager.TicksGame} 触发tick={Find.TickManager.TicksGame + 900000}");

                Find.LetterStack.ReceiveLetter(
                    "USAC_DebtSite_EscalationLetterLabel".Translate(),
                    "USAC_DebtSite_EscalationLetterText".Translate(contract.Label),
                    LetterDefOf.ThreatBig);
            }
        }

        // 收缴轮次结果评审
        public void CheckCollectionRoundResults(Map map)
        {
            if (map == null) return;

            // 检查剩余夹子数量
            int activeGrippers = 0;
            var grippers = map.listerThings.ThingsOfDef(USAC_DefOf.USAC_GripperIncoming);
            for (int i = 0; i < grippers.Count; i++)
            {
                if (!grippers[i].Destroyed) activeGrippers++;
            }

            // 等待最后一个夹子结束
            if (activeGrippers > 1) return;

            // 违约升级判定
            if (HasGripperDestroyedThisRound)
            {
                Log.Message("[USAC] 判定本轮收缴存在抗缴行为。");
                var debtComp = GameComponent_USACDebt.Instance;
                if (debtComp != null)
                {
                    var contract = debtComp.ContractManager.NextDueContract;
                    if (contract != null)
                    {
                        HandleCollectionFailure(contract, map);
                    }
                }
                HasGripperDestroyedThisRound = false;
            }
        }

        // 检查地图屏蔽状态
        private static bool IsMapSealedFromOrbit(Map map)
        {
            return MapRoofUtility.IsMapSealedFromOrbit(map);
        }
        #endregion
    }
}
