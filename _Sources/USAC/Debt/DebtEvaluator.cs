using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace USAC
{
    // 债务评估工具类
    public static class DebtEvaluator
    {
        #region 贷款评估
        // 贷款风险定价评估
        public static UnifiedLoanEval EvaluateLoan(
            int creditScore,
            float totalDebt,
            bool isSystemLocked,
            float interestRate,
            float growthRate,
            DebtGrowthMode growthMode)
        {
            float wealth = GameComponent_USACDebt.GetRichestPlayerHomeMap()?.wealthWatcher?.WealthTotal ?? 0f;

            // 基础信用系数
            float baseCreditFactor = Mathf.Lerp(0.1f, 0.3f, (creditScore - 30f) / 70f);
            if (creditScore < 30) baseCreditFactor = 0f;

            // 利率参数加成
            float interestBonus = interestRate * 1.5f;

            // 风险参数加成
            float growthBonus = growthRate * 2.5f;

            // 结算综合倍率
            float totalMult = baseCreditFactor + interestBonus + growthBonus;

            // 环境信用折扣
            float creditDiscount = Mathf.Clamp01((creditScore - 30) / 175f) * 0.30f;
            float actualInterest = Mathf.Round(interestRate * (1f - creditDiscount) * 1000f) / 1000f;

            // 计算可用额度
            float rawMax = wealth * totalMult - totalDebt;
            float maxAmount = Mathf.Floor(Mathf.Max(0f, rawMax) / 1000f) * 1000f;

            string blockReason = null;
            if (isSystemLocked)
                blockReason = "USAC_DebtSite_LoanLockedWarning".Translate();
            else if (creditScore < 30)
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
                return (GameComponent_USACDebt.GetRichestPlayerHomeMap()?.wealthWatcher?.WealthTotal ?? 0f) * c.GrowthRate;
            return c.Principal * c.GrowthRate;
        }
        #endregion
    }
}
