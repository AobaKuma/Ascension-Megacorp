using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace USAC
{
    // 配置游戏剧本债务合同
    public class ScenPart_USACDebt : ScenPart
    {
        public float initialDebt = 10000f;
        public DebtType debtType = DebtType.WholeMortgage;
        public DebtGrowthMode growthMode = DebtGrowthMode.WealthBased;
        public float growthRate = 0.20f;
        public float interestRate = 0.05f;

        private string initialDebtBuffer;
        private string growthRateBuffer;
        private string interestRateBuffer;

        public override string Summary(Scenario scen)
        {
            string typeStr = GetTypeLabel(debtType);
            string modeStr = growthMode == DebtGrowthMode.WealthBased
                ? "财富基准" : "本金基准";

            return $"USAC {typeStr}: ₿{initialDebt:N0}\n" +
                   $"周期增长: {growthRate * 100:F0}% ({modeStr})\n" +
                   $"周期利率: {interestRate * 100:F0}%";
        }

        public override void DoEditInterface(Listing_ScenEdit listing)
        {
            Rect rect = listing.GetScenPartRect(
                this, RowHeight * 6f);
            Listing_Standard sub = new Listing_Standard();
            sub.Begin(rect);

            // 初始本金
            sub.TextFieldNumericLabeled(
                "初始债务本金: ", ref initialDebt,
                ref initialDebtBuffer, 0f, 10000000f);

            // 贷款类型
            if (sub.ButtonTextLabeled(
                "贷款类型: ", GetTypeLabel(debtType)))
            {
                var options = new List<FloatMenuOption>
                {
                    new("整体抵押",
                        () => debtType = DebtType.WholeMortgage),
                    new("动态信贷 (预留)",
                        () => debtType = DebtType.DynamicLoan)
                };
                Find.WindowStack.Add(new FloatMenu(options));
            }

            // 增长模式
            if (sub.ButtonTextLabeled(
                "增长基准: ", growthMode.ToString()))
            {
                var options = new List<FloatMenuOption>
                {
                    new("USAC.UI.Assets.GrowthMode.WealthBased".Translate(0).RawText,
                        () => growthMode = DebtGrowthMode.WealthBased),
                    new("USAC.UI.Assets.GrowthMode.PrincipalBased".Translate(0).RawText,
                        () => growthMode = DebtGrowthMode.PrincipalBased)
                };
                Find.WindowStack.Add(new FloatMenu(options));
            }

            // 增长率与利率
            float gPct = growthRate * 100f;
            sub.TextFieldNumericLabeled(
                "周期增长率 (%): ", ref gPct,
                ref growthRateBuffer, 0f, 200f);
            growthRate = gPct / 100f;

            float iPct = interestRate * 100f;
            sub.TextFieldNumericLabeled(
                "周期利率 (%): ", ref iPct,
                ref interestRateBuffer, 0f, 200f);
            interestRate = iPct / 100f;

            sub.End();
        }

        public override void PostGameStart()
        {
            var comp = GameComponent_USACDebt.Instance;
            if (comp == null) return;

            var contract = new DebtContract(
                debtType, initialDebt,
                growthRate, interestRate, growthMode);

            comp.ActiveContracts.Add(contract);
            comp.AddTransaction(USACTransactionType.Initial,
                initialDebt,
                $"开局剧本 {GetTypeLabel(debtType)}");
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref initialDebt,
                "initialDebt", 10000f);
            Scribe_Values.Look(ref debtType,
                "debtType", DebtType.WholeMortgage);
            Scribe_Values.Look(ref growthMode,
                "growthMode", DebtGrowthMode.WealthBased);
            Scribe_Values.Look(ref growthRate,
                "growthRate", 0.20f);
            Scribe_Values.Look(ref interestRate,
                "interestRate", 0.05f);
        }

        private static string GetTypeLabel(DebtType t)
        {
            return t switch
            {
                DebtType.WholeMortgage => "整体抵押",
                DebtType.DynamicLoan => "动态信贷",
                _ => "未知"
            };
        }
    }
}
