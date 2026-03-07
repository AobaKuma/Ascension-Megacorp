using RimWorld;
using Verse;

namespace USAC
{
    [DefOf]
    public static class USAC_HistoryEventDefOf
    {
        public static HistoryEventDef USAC_Coverup;
        public static HistoryEventDef USAC_HostilityReset;

        static USAC_HistoryEventDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(USAC_HistoryEventDefOf));
        }
    }
}
