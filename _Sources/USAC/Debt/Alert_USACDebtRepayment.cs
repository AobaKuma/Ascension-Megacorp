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
            defaultLabel = "USAC 债务结算";
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
            if (next == null) return "USAC 债务结算";

            int ticksLeft = next.NextCycleTick
                - Find.TickManager.TicksGame;
            float days = Mathf.Max(0f, ticksLeft / 60000f);

            return $"USAC 结算: {days:F1}天 ({comp.ActiveCount}笔)";
        }

        public override TaggedString GetExplanation()
        {
            var comp = GameComponent_USACDebt.Instance;
            if (comp == null) return "";

            Map map = Find.AnyPlayerHomeMap;
            int bonds = map != null
                ? comp.GetBondCountNearBeacons(map)
                : 0;

            string result = $"信用评分: {comp.CreditScore}\n" +
                            $"信标范围债券: {bonds}张\n\n";

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

                result +=
                    $"▸ {c.Label}\n" +
                    $"  本金: ₿{c.Principal:N0}" +
                    $"  预估利息: ₿{estInterest:N0}\n" +
                    $"  到期: {days:F1}天" +
                    $"  欠缴: {c.MissedPayments}次\n\n";
            }

            var next = comp.NextDueContract;
            if (next != null)
            {
                int tl = next.NextCycleTick
                    - Find.TickManager.TicksGame;
                if (tl < 180000)
                {
                    result += "⚠ 最近合同即将到期！" +
                              "请准备足够USAC债券。";
                }
            }

            return result;
        }
    }
}
