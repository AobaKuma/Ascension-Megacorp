using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;

namespace USAC
{
    // 拦截窗口添加过程实现重定向
    [HarmonyPatch(typeof(WindowStack), nameof(WindowStack.Add))]
    public static class Patch_USACTradeWindow
    {
        // 递归保护防止重复重定向
        private static bool recursionGuard = false;

        public static bool Prefix(Window window)
        {
            if (recursionGuard) return true;

            // 仅对贸易对话框进行重定向检查
            if (window is Dialog_Trade tradeDialog)
            {
                // 确保对话框为USAC商船类型
                if (TradeSession.Active && TradeSession.trader?.TraderKind != null)
                {
                    var ext = TradeSession.trader.TraderKind.GetModExtension<ModExtension_CorpseBagTrader>();
                    if (ext != null && ext.useCorpseBagCurrency)
                    {
                        // 获取协议控制逻辑
                        var settings = USAC_Mod.Settings;
                        if (settings == null) return true;

                        // 场景一已处理且接受转至终端
                        if (settings.termsProcessed && settings.hasAcceptedTerms)
                        {
                            RedirectTo(new Dialog_USACTerminal(GetGiftsOnly(tradeDialog)));
                            return false; 
                        }

                        // 场景二未处理协议弹出协议书
                        if (!settings.termsProcessed)
                        {
                            RedirectTo(new Dialog_USACTermsOfService(GetGiftsOnly(tradeDialog), tradeDialog));
                            return false;
                        }

                        // 场景三已处理且拒绝放行原版
                        // 直接返回 true 即可
                    }
                }
            }

            return true;
        }

        private static void RedirectTo(Window newWindow)
        {
            recursionGuard = true;
            try
            {
                Find.WindowStack.Add(newWindow);
            }
            finally
            {
                recursionGuard = false;
            }
        }

        private static bool GetGiftsOnly(Dialog_Trade dialog)
        {
            // 反射获取giftsOnly字段
            return (bool)AccessTools.Field(typeof(Dialog_Trade), "giftsOnly").GetValue(dialog);
        }
    }
}
