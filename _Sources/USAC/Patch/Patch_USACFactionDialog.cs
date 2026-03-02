using HarmonyLib;
using RimWorld;
using Verse;

namespace USAC
{
    // 注入债务门户入口
    // 拦截通讯启动门户
    [HarmonyPatch(typeof(Faction), nameof(Faction.TryOpenComms))]
    public static class Patch_USACFactionDialog
    {
        [HarmonyPrefix]
        public static bool Prefix(Faction __instance, Pawn negotiator)
        {
            if (__instance.def == USAC_FactionDefOf.USAC_Faction)
            {
                // 直接启动综合门户 UI
                Find.WindowStack.Add(new Dialog_USACPortal());

                // 拦截原版通讯逻辑
                // 防止创建通讯对话框
                return false;
            }
            return true;
        }
    }
}
