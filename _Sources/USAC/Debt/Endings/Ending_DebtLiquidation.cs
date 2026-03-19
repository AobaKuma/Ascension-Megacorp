using System.Text;
using System.Linq;
using RimWorld;
using Verse;

namespace USAC.Endings
{
    // 地图抽空触发债务清算结局
    public static class Ending_DebtLiquidation
    {
        #region 触发入口
        public static void TriggerEnding()
        {
            var comp = GameComponent_USACDebt.Instance;
            if (comp == null || comp.EndingTriggered) return;

            // 标记结局启动防止重入
            comp.EndingTriggered = true;

            // 重置所有债务合同
            ClearAllDebt(comp);

            LongEventHandler.QueueLongEvent(() =>
            {
                string text = BuildCreditsText();
                // 允许继续游玩等待流浪者
                GameVictoryUtility.ShowCredits(text, null, exitToMainMenu: false);
            }, "USAC.Ending.Liquidation.Loading", false, null);
        }
        #endregion

        #region 债务清除
        private static void ClearAllDebt(GameComponent_USACDebt comp)
        {
            // 将所有合同标记为失效
            if (comp.ActiveContracts != null)
            {
                foreach (var c in comp.ActiveContracts)
                {
                    c.IsActive = false;
                    c.Principal = 0;
                    c.AccruedInterest = 0;
                }
            }

            // 重置系统允许新殖民者游玩
            comp.CreditScore = 50;
            comp.IsSystemLocked = false;
            comp.EndingTriggered = false;
        }
        #endregion

        #region 字幕文本生成
        private static string BuildCreditsText()
        {
            var sb = new StringBuilder();
            var comp = GameComponent_USACDebt.Instance;

            // 主标题
            sb.AppendLine("USAC.Ending.Liquidation.Title".Translate());
            sb.AppendLine();
            sb.AppendLine("USAC.Ending.Liquidation.Intro".Translate());
            sb.AppendLine();
            sb.AppendLine("USAC.Ending.Liquidation.Body".Translate());
            sb.AppendLine();

            // 财务清算概览
            if (comp != null)
            {
                sb.AppendLine("USAC.Ending.Liquidation.FinancialReport".Translate());
                sb.AppendLine($"   - {"USAC.UI.Assets.CreditScore".Translate()}: {comp.CreditScore}");
                sb.AppendLine($"   - {"USAC.Ending.Liquidation.TotalDebtLiquidated".Translate()}: ₿{comp.TotalDebt:N0}");
                sb.AppendLine();
            }

            sb.AppendLine("USAC.Ending.Liquidation.Close".Translate());

            // 利用插槽显示人员并自定义文案
            string namesList = string.Empty;
            if (comp?.LiquidatedPawns != null && comp.LiquidatedPawns.Count > 0)
            {
                namesList = comp.LiquidatedPawns.Select(p => p.LabelCap.ToString()).ToList().ToCommaList(true);
            }

            return GameVictoryUtility.MakeEndCredits(
                sb.ToString(), 
                "USAC.Ending.Liquidation.Ending".Translate(),
                namesList, 
                "USAC.Ending.Liquidation.CapturedAssets",
                comp?.LiquidatedPawns);
        }
        #endregion
    }
}
