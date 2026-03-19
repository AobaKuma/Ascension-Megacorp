using HarmonyLib;
using Verse;

namespace USAC
{
    // 静态构造并应用补丁
    [StaticConstructorOnStartup]
    public static class HarmonyEntry
    {
        static HarmonyEntry()
        {
            var harmony = new Harmony("AOBA.USAC");
            harmony.PatchAll();
            USAC_Debug.Log("[USAC] Harmony 补丁已应用");
        }
    }
}
