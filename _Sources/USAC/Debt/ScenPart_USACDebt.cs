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
                ? "USAC.Debt.GrowthMode.WealthBase".Translate()
                : "USAC.Debt.GrowthMode.PrincipalBase".Translate();

            return "USAC.Debt.ScenPart.Summary".Translate(
                typeStr,
                initialDebt.ToString("N0"),
                (growthRate * 100f).ToString("F0"),
                modeStr,
                (interestRate * 100f).ToString("F0"));
        }

        public override void DoEditInterface(Listing_ScenEdit listing)
        {
            Rect rect = listing.GetScenPartRect(
                this, RowHeight * 6f);
            Listing_Standard sub = new Listing_Standard();
            sub.Begin(rect);

            // 初始本金
            sub.TextFieldNumericLabeled(
                "USAC.Debt.ScenPart.InitialDebt".Translate(), ref initialDebt,
                ref initialDebtBuffer, 0f, 10000000f);

            // 贷款类型
            if (sub.ButtonTextLabeled(
                "USAC.Debt.ScenPart.DebtType".Translate(), GetTypeLabel(debtType)))
            {
                var options = new List<FloatMenuOption>
                {
                    new("USAC.Debt.Type.WholeMortgage".Translate(),
                        () => debtType = DebtType.WholeMortgage),
                    new("USAC.Debt.ScenPart.DynamicLoanReserved".Translate(),
                        () => debtType = DebtType.DynamicLoan)
                };
                Find.WindowStack.Add(new FloatMenu(options));
            }

            // 增长模式
            if (sub.ButtonTextLabeled(
                "USAC.Debt.ScenPart.GrowthBase".Translate(),
                growthMode == DebtGrowthMode.WealthBased
                    ? "USAC.Debt.GrowthMode.WealthBase".Translate()
                    : "USAC.Debt.GrowthMode.PrincipalBase".Translate()))
            {
                var options = new List<FloatMenuOption>
                {
                    new("USAC.Debt.GrowthMode.WealthBase".Translate(),
                        () => growthMode = DebtGrowthMode.WealthBased),
                    new("USAC.Debt.GrowthMode.PrincipalBase".Translate(),
                        () => growthMode = DebtGrowthMode.PrincipalBased)
                };
                Find.WindowStack.Add(new FloatMenu(options));
            }

            // 增长率与利率
            float gPct = growthRate * 100f;
            sub.TextFieldNumericLabeled(
                "USAC.Debt.ScenPart.GrowthRatePct".Translate(), ref gPct,
                ref growthRateBuffer, 0f, 200f);
            growthRate = gPct / 100f;

            float iPct = interestRate * 100f;
            sub.TextFieldNumericLabeled(
                "USAC.Debt.ScenPart.InterestRatePct".Translate(), ref iPct,
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
                "USAC.Debt.Transaction.ScenarioStart".Translate(GetTypeLabel(debtType)));
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
                DebtType.WholeMortgage =>
                    "USAC.Debt.Type.WholeMortgage".Translate(),
                DebtType.DynamicLoan =>
                    "USAC.Debt.Type.DynamicLoan".Translate(),
                _ => "USAC.Debt.Type.Unknown".Translate()
            };
        }
    }
}
