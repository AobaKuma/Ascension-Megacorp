using RimWorld;
using Verse;
using UnityEngine;

namespace USAC
{
    // 显示合同结算倒计时
    public class Alert_USACDebtRepayment : Alert
    {
        // 帧级缓存减少重复查询
        private GameComponent_USACDebt cachedComp;
        private DebtContract cachedNext;
        private int cachedActiveCount;
        private int lastCacheTick = -1;

        public Alert_USACDebtRepayment()
        {
            defaultLabel = "USAC.Alert.DebtRepayment.Label".Translate();
        }

        // 刷新帧级缓存
        private void RefreshCache()
        {
            int tick = Find.TickManager?.TicksGame ?? -1;
            if (tick == lastCacheTick) return;
            lastCacheTick = tick;
            cachedComp = GameComponent_USACDebt.Instance;
            if (cachedComp == null) { cachedNext = null; cachedActiveCount = 0; return; }
            cachedNext = cachedComp.NextDueContract;
            cachedActiveCount = cachedComp.ActiveCount;
        }

        public override AlertReport GetReport()
        {
            RefreshCache();
            if (cachedComp == null || cachedActiveCount <= 0) return false;

            // 系统锁定时不显示提醒
            if (cachedComp.IsSystemLocked)
            {
                var contracts = cachedComp.ActiveContracts;
                for (int i = 0; i < contracts.Count; i++)
                {
                    if (contracts[i].IsActive && contracts[i].ConsecutiveCollectionFails >= 2)
                        return false;
                }
            }

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
                if (cachedNext == null) return AlertPriority.Medium;
                int ticksLeft = cachedNext.NextCycleTick - Find.TickManager.TicksGame;
                return ticksLeft < 180000 ? AlertPriority.High : AlertPriority.Medium;
            }
        }

        public override string GetLabel()
        {
            if (cachedNext == null) return "USAC.Alert.DebtRepayment.Label".Translate();
            int ticksLeft = cachedNext.NextCycleTick - Find.TickManager.TicksGame;
            float days = Mathf.Max(0f, ticksLeft / 60000f);
            return "USAC.Alert.DebtRepayment.LabelWithTime"
                .Translate(days.ToString("F1"), cachedActiveCount);
        }

        public override TaggedString GetExplanation()
        {
            if (cachedComp == null) return "";

            Map map = Find.AnyPlayerHomeMap;
            int bonds = map != null ? cachedComp.GetBondCountNearBeacons(map) : 0;

            string result = "USAC.Alert.DebtRepayment.Explanation.Header"
                .Translate(cachedComp.CreditScore, bonds);

            // 按周期排序遍历
            var contracts = cachedComp.ActiveContracts;
            for (int i = 0; i < contracts.Count; i++)
            {
                var c = contracts[i];
                if (!c.IsActive) continue;

                int ticksLeft = c.NextCycleTick - Find.TickManager.TicksGame;
                float days = Mathf.Max(0f, ticksLeft / 60000f);
                float estInterest = DebtContract.CeilTo1000(c.Principal * c.InterestRate);

                result += "USAC.Alert.DebtRepayment.Explanation.ContractEntry"
                    .Translate(
                        c.Label,
                        c.Principal.ToString("N0"),
                        estInterest.ToString("N0"),
                        days.ToString("F1"),
                        c.MissedPayments);
            }

            if (cachedNext != null)
            {
                int tl = cachedNext.NextCycleTick - Find.TickManager.TicksGame;
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
