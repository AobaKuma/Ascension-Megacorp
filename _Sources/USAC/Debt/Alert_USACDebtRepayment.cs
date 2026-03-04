using System.Linq;
using RimWorld;
using Verse;
using UnityEngine;

namespace USAC
{
    // 显示合同结算倒计时
    public class Alert_USACDebtRepayment : Alert
    {
        public Alert_USACDebtRepayment()
        {
            defaultLabel = "USAC.Alert.DebtRepayment.Label".Translate();
        }

        public override AlertReport GetReport()
        {
            var comp = GameComponent_USACDebt.Instance;
            if (comp == null || comp.ActiveCount <= 0) return false;
            return true;
        }

        protected override void OnClick()
        {
            Find.WindowStack.Add(new Dialog_USACPortal());
        }

        public override AlertPriority Priority
        {
            get
            {
                var comp = GameComponent_USACDebt.Instance;
                var next = comp?.NextDueContract;
                if (next == null) return AlertPriority.Medium;

                int ticksLeft = next.NextCycleTick
                    - Find.TickManager.TicksGame;
                if (ticksLeft < 180000) return AlertPriority.High;
                return AlertPriority.Medium;
            }
        }

        public override string GetLabel()
        {
            var comp = GameComponent_USACDebt.Instance;
            var next = comp?.NextDueContract;
            if (next == null) return "USAC.Alert.DebtRepayment.Label".Translate();

            int ticksLeft = next.NextCycleTick
                - Find.TickManager.TicksGame;
            float days = Mathf.Max(0f, ticksLeft / 60000f);

            return "USAC.Alert.DebtRepayment.LabelWithTime"
                .Translate(days.ToString("F1"), comp.ActiveCount);
        }

        public override TaggedString GetExplanation()
        {
            var comp = GameComponent_USACDebt.Instance;
            if (comp == null) return "";

            Map map = Find.AnyPlayerHomeMap;
            int bonds = map != null
                ? comp.GetBondCountNearBeacons(map)
                : 0;

            string result = "USAC.Alert.DebtRepayment.Explanation.Header"
                .Translate(comp.CreditScore, bonds);

            var contracts = comp.ActiveContracts
                .Where(c => c.IsActive)
                .OrderBy(c => c.NextCycleTick);

            foreach (var c in contracts)
            {
                int ticksLeft = c.NextCycleTick
                    - Find.TickManager.TicksGame;
                float days = Mathf.Max(0f, ticksLeft / 60000f);
                float estInterest = DebtContract.CeilTo1000(
                    c.Principal * c.InterestRate);

                result += "USAC.Alert.DebtRepayment.Explanation.ContractEntry"
                    .Translate(
                        c.Label,
                        c.Principal.ToString("N0"),
                        estInterest.ToString("N0"),
                        days.ToString("F1"),
                        c.MissedPayments);
            }

            var next = comp.NextDueContract;
            if (next != null)
            {
                int tl = next.NextCycleTick
                    - Find.TickManager.TicksGame;
                if (tl < 180000)
                {
                    result += "USAC.Alert.DebtRepayment.Explanation.ImminentWarning"
                        .Translate();
                }
            }

            return result;
        }
    }
}
