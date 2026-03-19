using RimWorld;
using Verse;

namespace USAC
{
    // 机兵整备组件属性
    public class CompProperties_MechReadiness : CompProperties
    {
        // 机兵整备容量数值
        public float capacity = 100f;

        // 机兵整备日损耗值
        public float consumptionPerDay = 10f;

        // 整备补给物品定义
        public ThingDef supplyDef;

        // 低整备状态阈值
        public float lowThreshold = 0.3f;

        // 低整备状态异常定义
        public HediffDef lowReadinessHediff;

        public CompProperties_MechReadiness()
        {
            compClass = typeof(CompMechReadiness);
        }
    }

    // 机兵整备逻辑组件
    public class CompMechReadiness : ThingComp
    {
        public CompProperties_MechReadiness Props => (CompProperties_MechReadiness)props;
        public bool autoResupply = true;

        private Pawn Pawn => parent as Pawn;

        public float Readiness => Pawn?.needs?.TryGetNeed<Need_Readiness>()?.CurLevel ?? 0f;
        public float ReadinessPercent => Readiness / Props.capacity;
        public bool IsLowReadiness => Readiness <= 0f;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref autoResupply, "autoResupply", true);
        }

        public override System.Collections.Generic.IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo g in base.CompGetGizmosExtra()) yield return g;
            if (Pawn != null && Pawn.Faction == Faction.OfPlayer)
            {
                yield return new Command_Toggle
                {
                    defaultLabel = "USAC_AutoResupply".Translate(),
                    defaultDesc = "USAC_AutoResupplyDesc".Translate(),
                    icon = TexCommand.RearmTrap,
                    isActive = () => autoResupply,
                    toggleAction = () => autoResupply = !autoResupply
                };
            }
        }

        public override string CompInspectStringExtra()
        {
            var need = Pawn?.needs?.TryGetNeed<Need_Readiness>();
            if (need == null) return null;
            return "USAC_Readiness".Translate() + ": " + need.CurLevel.ToString("F0") + " / " + Props.capacity.ToString("F0");
        }
    }
}
