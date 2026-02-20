using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using System;

namespace USAC
{
    // 限制派系好感度变动
    // 拦截好感度增益逻辑
    [HarmonyPatch(typeof(Faction), "TryAffectGoodwillWith", new Type[] {
        typeof(Faction), typeof(int), typeof(bool), typeof(bool), typeof(HistoryEventDef), typeof(GlobalTargetInfo?)
    })]
    public static class Patch_Faction_TryAffectGoodwillWith
    {
        [HarmonyPrefix]
        public static void Prefix(Faction __instance, Faction other, ref int goodwillChange, HistoryEventDef reason)
        {
            // 校验增量涉及派系
            if (goodwillChange <= 0) return;

            // 识别项目关联派系
            bool isUSAC = IsUSACFaction(__instance) || IsUSACFaction(other);

            if (isUSAC)
            {
                // 过滤任务与调试增益
                if (reason != HistoryEventDefOf.QuestGoodwillReward &&
                    reason != HistoryEventDefOf.DebugGoodwill)
                {
                    // 修正常规交互增益
                    goodwillChange = 0;
                }
            }
        }

        private static bool IsUSACFaction(Faction faction)
        {
            if (faction?.def == null) return false;
            // 匹配项目派系定义
            return faction.def == USAC_FactionDefOf.USAC_Faction || faction.def.categoryTag == "USAC";
        }
    }
}
