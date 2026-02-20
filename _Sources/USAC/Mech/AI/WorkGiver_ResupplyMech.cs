using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace USAC
{
    // 定义机兵整备分配节点
    public class WorkGiver_ResupplyMech : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.Pawn);
        public override PathEndMode PathEndMode => PathEndMode.Touch;
        public override Danger MaxPathDanger(Pawn pawn) => Danger.Deadly;

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            return pawn.Map.mapPawns.SpawnedPawnsInFaction(pawn.Faction);
        }

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return !MechanitorUtility.IsMechanitor(pawn);
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!ModLister.CheckBiotech("Repair mech")) return false;

            Pawn mech = t as Pawn;
            if (mech == null || !mech.RaceProps.IsMechanoid || mech.Faction != pawn.Faction) return false;

            CompMechReadiness comp = mech.TryGetComp<CompMechReadiness>();
            if (comp == null || comp.ReadinessPercent >= 1f) return false;

            if (mech.InAggroMentalState || mech.HostileTo(pawn)) return false;
            if (mech.IsBurning() || mech.IsAttacking()) return false;

            if (!pawn.CanReserve(mech, 1, -1, null, forced)) return false;

            // 阈值保护过滤
            if (!forced && comp.Readiness > comp.Props.capacity * 0.75f) return false;

            // 自动开关限制
            if (!forced && !comp.autoResupply) return false;

            Thing supply = FindSupply(pawn, comp);
            if (supply == null) return false;

            return true;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Pawn mech = (Pawn)t;
            CompMechReadiness comp = mech.TryGetComp<CompMechReadiness>();
            Thing supply = FindSupply(pawn, comp);

            Job job = JobMaker.MakeJob(USAC_DefOf.USAC_ResupplyMech, mech, supply);
            job.count = CalcToConsume(comp);
            return job;
        }

        // 计算实际消耗零件数
        private int CalcToConsume(CompMechReadiness comp)
        {
            float needed = comp.Props.capacity - comp.Readiness;
            float restorePerItem = comp.Props.capacity * 0.25f;
            int n = UnityEngine.Mathf.CeilToInt(needed / restorePerItem);
            float waste = n * restorePerItem - needed;
            // 浪费超过5%则少取一个零件
            if (waste > comp.Props.capacity * 0.05f && n > 1) n--;
            return n;
        }

        private Thing FindSupply(Pawn pawn, CompMechReadiness comp)
        {
            System.Predicate<Thing> validator = (Thing x) => !x.IsForbidden(pawn) && pawn.CanReserve(x);
            return GenClosest.ClosestThingReachable(pawn.Position, pawn.Map, ThingRequest.ForDef(comp.Props.supplyDef), PathEndMode.ClosestTouch, TraverseParms.For(pawn), 9999f, validator);
        }
    }
}
