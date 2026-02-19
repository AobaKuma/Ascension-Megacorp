using RimWorld;
using Verse;

namespace USAC
{
    [DefOf]
    public static class USAC_DefOf
    {
        // 引用机兵空投定义
        public static ThingDef USAC_MechIncoming;

        // 引用信用债券定义
        public static ThingDef USAC_Bond;

        // 引用视觉特效定义
        public static FleckDef USAC_WastewaterDroplet;

        // 引用工作作业定义
        public static JobDef USAC_UseItemOnTarget;

        // 引用火箭排雷索定义
        public static ThingDef USAC_MICLIC_Segment;

        public static JobDef USAC_WaitDetonate;

        static USAC_DefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(USAC_DefOf));
        }
    }
}
