using HarmonyLib;
using RimWorld;
using System;
using Verse;

namespace USAC
{
    [HarmonyPatch(typeof(CompShuttle), "get_AllRequiredThingsLoaded")]
    public static class Patch_ShuttleNullGuard
    {
        public static Exception Finalizer(Exception __exception, CompShuttle __instance, ref bool __result)
        {
            if (__exception is not NullReferenceException)
                return __exception;

            try
            {
                if (__instance?.requiredPawns != null)
                    __instance.requiredPawns.RemoveAll(p => p == null);

                if (__instance?.pawnsToIgnoreIfDownedOfNotOnTheMap != null)
                    __instance.pawnsToIgnoreIfDownedOfNotOnTheMap.RemoveAll(p => p == null);

                if (__instance?.requiredItems != null)
                    __instance.requiredItems.RemoveAll(i => i.ThingDef == null || i.Count <= 0);

                __result = false;
                Log.Warning("[USAC] Prevented NullReferenceException in CompShuttle.AllRequiredThingsLoaded; malformed shuttle requirements were sanitized.");
                return null;
            }
            catch (Exception sanitizeEx)
            {
                Log.Warning($"[USAC] Shuttle null-guard failed while handling exception: {sanitizeEx}");
                __result = false;
                return null;
            }
        }
    }
}