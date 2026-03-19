using RimWorld;
using UnityEngine;
using Verse;
using USAC.Endings;

namespace USAC
{
    // 显示围攻准备倒计时或防守剩余时间
    public class Alert_USACSiegeCountdown : Alert
    {
        public Alert_USACSiegeCountdown()
        {
            // 预设标签
            defaultLabel = "USAC.Alert.SiegeCountdown.Label".Translate();
        }

        public override AlertReport GetReport()
        {
            var inst = GameComponent_DebtTransfer.Instance;
            if (inst == null) return false;

            // 仅在备战或防守阶段显示
            return inst.Phase == GameComponent_DebtTransfer.TransferPhase.Countdown ||
                   inst.Phase == GameComponent_DebtTransfer.TransferPhase.UnderSiege;
        }

        public override string GetLabel()
        {
            var inst = GameComponent_DebtTransfer.Instance;
            if (inst == null) return base.GetLabel();

            if (inst.Phase == GameComponent_DebtTransfer.TransferPhase.UnderSiege)
                return "USAC.Alert.SiegeCountdown.LabelActive".Translate();

            return "USAC.Alert.SiegeCountdown.Label".Translate();
        }

        public override TaggedString GetExplanation()
        {
            var inst = GameComponent_DebtTransfer.Instance;
            if (inst == null) return "";

            int now = Find.TickManager.TicksGame;
            string factionName = inst.BuyerFaction?.Name ?? "UNKNOWN";

            if (inst.Phase == GameComponent_DebtTransfer.TransferPhase.UnderSiege)
            {
                int totalRequiredTicks = inst.SiegeDaysRequired * GenDate.TicksPerDay;
                int remainingTicks = totalRequiredTicks - (now - inst.SiegeStartTick);
                string timeStr = GenDate.ToStringTicksToPeriod(Mathf.Max(0, remainingTicks));

                return "USAC.Alert.SiegeCountdown.ExplanationActive".Translate(timeStr, factionName);
            }

            if (inst.Phase == GameComponent_DebtTransfer.TransferPhase.Countdown)
            {
                int remainingTicks = inst.CountdownTargetTick - now;
                string timeStr = GenDate.ToStringTicksToPeriod(Mathf.Max(0, remainingTicks));

                return "USAC.Alert.SiegeCountdown.Explanation".Translate(timeStr, factionName);
            }

            return "";
        }

        public override AlertPriority Priority => AlertPriority.High;
    }
}
