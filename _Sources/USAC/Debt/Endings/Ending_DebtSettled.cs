using System.Text;
using RimWorld;
using Verse;

namespace USAC.Endings
{
    // 还清债务触发债务清偿结局
    public static class Ending_DebtSettled
    {
        #region 触发入口
        public static void TriggerEnding()
        {
            var comp = GameComponent_USACDebt.Instance;
            if (comp == null || comp.EndingTriggered) return;

            // 仅当所有债务都已结清时触发
            if (comp.ActiveCount > 0) return;

            // 标记结局已启动
            comp.EndingTriggered = true;

            LongEventHandler.QueueLongEvent(() =>
            {
                string text = BuildCreditsText();
                GameVictoryUtility.ShowCredits(text, null, exitToMainMenu: false);
            }, "USAC.Ending.Settled.Loading", false, null);
        }
        #endregion

        #region 字幕文本生成
        private static string BuildCreditsText()
        {
            var sb = new StringBuilder();

            // 主标题
            sb.AppendLine("USAC.Ending.Settled.Title".Translate());
            sb.AppendLine();
            sb.AppendLine("USAC.Ending.Settled.Intro".Translate());
            sb.AppendLine();

            // 财务收支汇报
            var comp = GameComponent_USACDebt.Instance;
            if (comp != null)
            {
                sb.AppendLine("USAC.Ending.Settled.CreditScore"
                    .Translate(comp.CreditScore));
            }
            sb.AppendLine();

            // 结尾
            sb.AppendLine("USAC.Ending.Settled.Close".Translate());

            return GameVictoryUtility.MakeEndCredits(
                sb.ToString(), "USAC.Ending.Settled.Ending".Translate(),
                string.Empty);
        }
        #endregion
    }
}
