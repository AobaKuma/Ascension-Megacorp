using RimWorld;
using Verse;

namespace USAC.Endings
{
    // 债务结局管理入口
    public static class USACEndingManager
    {
        #region 结局触发
        // 触发债务清偿结局
        public static void TriggerDebtSettled()
        {
            Ending_DebtSettled.TriggerEnding();
        }

        // 触发债务清算结局
        public static void TriggerDebtLiquidation()
        {
            Ending_DebtLiquidation.TriggerEnding();
        }

        // 记录已摧毁据点并检查债权转移
        public static void NotifyDebtSiteDestroyed(DebtContract contract)
        {
            var comp = GameComponent_USACDebt.Instance;
            if (comp == null) return;

            // 据点摧毁计数递增
            comp.DestroyedDebtSiteCount++;

            // 违约计数清零
            if (contract != null)
            {
                contract.ConsecutiveCollectionFails = 0;
                contract.HasActiveDebtSite = false;
            }

            // 刷新锁定状态
            comp.RefreshSystemLockStatus();

            // 摧毁据点达到上限触发防御胜利结局
            if (comp.DestroyedDebtSiteCount >= 4)
            {
                GameComponent_DebtTransfer.TriggerEnding(contract);
            }
        }
        #endregion

        #region 字幕工具
        // 显示黑底滚动结局字幕
        public static void ShowEndingCredits(string text, bool exitToMenu = false)
        {
            string full = GameVictoryUtility.MakeEndCredits(
                text, string.Empty, string.Empty);
            GameVictoryUtility.ShowCredits(full, null, exitToMenu);
        }
        #endregion
    }
}
