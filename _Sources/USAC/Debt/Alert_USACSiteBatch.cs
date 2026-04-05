using RimWorld;
using UnityEngine;
using Verse;

namespace USAC
{
    // 显示据点生成行动倒计时
    public class Alert_USACSiteBatch : Alert
    {
        public Alert_USACSiteBatch()
        {
            defaultLabel = "USAC.Alert.SiteBatch.Label".Translate();
        }

        public override AlertReport GetReport()
        {
            var comp = GameComponent_USACDebt.Instance;
            if (comp == null) return false;

            // 仅在系统锁定且处于据点模式时显示
            if (!comp.IsSystemLocked) return false;
            if (comp.TicksUntilNextSiteBatch <= 1) return false;

            // 至少有一个合同处于据点模式
            var contracts = comp.ActiveContracts;
            for (int i = 0; i < contracts.Count; i++)
            {
                if (contracts[i].IsActive && contracts[i].IsInSiteMode)
                    return true;
            }
            return false;
        }

        public override string GetLabel()
        {
            var comp = GameComponent_USACDebt.Instance;
            if (comp == null) return base.GetLabel();

            float days = comp.TicksUntilNextSiteBatch / 60000f;
            if (days < 0.1f) days = 0.1f;
            return "USAC.Alert.SiteBatch.LabelWithTime".Translate(days.ToString("F1"));
        }

        public override TaggedString GetExplanation()
        {
            var comp = GameComponent_USACDebt.Instance;
            if (comp == null) return "";

            int ticks = comp.TicksUntilNextSiteBatch;
            float days = ticks / 60000f;
            string timeStr = GenDate.ToStringTicksToPeriod(Mathf.Max(0, ticks), false);

            return "USAC.Alert.SiteBatch.Explanation".Translate(timeStr);
        }

        public override AlertPriority Priority => AlertPriority.High;
    }
}
