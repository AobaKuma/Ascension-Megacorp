using RimWorld;
using Verse;

namespace USAC
{
    public class CompProperties_MechWreck : CompProperties
    {
        public ThingDef wreckDef;

        public CompProperties_MechWreck()
        {
            compClass = typeof(CompMechWreck);
        }
    }

    public class CompMechWreck : ThingComp
    {
        private Rot4 cachedRotation = Rot4.Invalid;

        public CompProperties_MechWreck Props => (CompProperties_MechWreck)props;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (parent is Pawn pawn)
            {
                cachedRotation = pawn.Rotation;
            }
        }

        public override void CompTick()
        {
            base.CompTick();
            if (parent is Pawn pawn && pawn.Spawned)
            {
                cachedRotation = pawn.Rotation;
            }
        }

        public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
        {
            base.Notify_Killed(prevMap, dinfo);

            if (Props.wreckDef == null || prevMap == null) return;
            if (!(parent is Pawn pawn)) return;

            IntVec3 deathPos = pawn.Position;
            Rot4 rotation = cachedRotation.IsValid ? cachedRotation : Rot4.South;

            Thing wreck = ThingMaker.MakeThing(Props.wreckDef);

            var parentPaint = pawn.TryGetComp<Fortified.CompPaintable>();
            var wreckPaint = wreck.TryGetComp<Fortified.CompPaintable>();
            if (parentPaint != null && wreckPaint != null)
            {
                wreckPaint.color1 = parentPaint.color1;
                wreckPaint.color2 = parentPaint.color2;
                wreckPaint.color3 = parentPaint.color3;
                wreckPaint.camoDef = parentPaint.camoDef;
                wreckPaint.brightness = parentPaint.brightness;
                // 锁定数据防止覆盖
                wreckPaint.initialized = true;
            }

            if (pawn.Faction != null)
            {
                wreck.SetFaction(pawn.Faction);
            }

            // 寻找近处可用位置生成残骸
            if (!GenPlace.TryPlaceThing(wreck, deathPos, prevMap, ThingPlaceMode.Near, out _, null, null, rotation))
            {
                GenSpawn.Spawn(wreck, deathPos, prevMap, rotation);
            }

            if (pawn.Corpse != null && !pawn.Corpse.Destroyed)
            {
                pawn.Corpse.Destroy();
            }
        }
    }
}
