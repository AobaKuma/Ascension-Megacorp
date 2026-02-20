using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace USAC
{
    // 定义机兵整备作业驱动类
    public class JobDriver_ResupplyMech : JobDriver
    {
        private const TargetIndex MechInd = TargetIndex.A;
        private const TargetIndex SupplyInd = TargetIndex.B;
        private const int ResupplyDuration = 240;

        protected Pawn Mech => (Pawn)job.GetTarget(MechInd).Thing;
        protected Thing Supply => job.GetTarget(SupplyInd).Thing;
        protected CompMechReadiness Readiness => Mech.TryGetComp<CompMechReadiness>();

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (pawn.Reserve(Mech, job, 1, -1, null, errorOnFailed))
            {
                return pawn.Reserve(Supply, job, 1, -1, null, errorOnFailed);
            }
            return false;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(MechInd);

            // 状态已满则跳过
            AddFailCondition(() => Readiness == null || Readiness.ReadinessPercent >= 1f);

            // 提取目标物品数量
            yield return Toils_General.DoAtomic(delegate
            {
                job.count = GetRequiredSupplyCount();
            });

            Toil reserveSupply = Toils_Reserve.Reserve(SupplyInd);
            yield return reserveSupply;

            yield return Toils_Goto.GotoThing(SupplyInd, PathEndMode.ClosestTouch)
                .FailOnDespawnedNullOrForbidden(SupplyInd)
                .FailOnSomeonePhysicallyInteracting(SupplyInd);

            yield return Toils_Haul.StartCarryThing(SupplyInd, false, true)
                .FailOnDestroyedNullOrForbidden(SupplyInd);

            yield return Toils_Haul.CheckForGetOpportunityDuplicate(reserveSupply, SupplyInd, TargetIndex.None, true);

            yield return Toils_Goto.GotoThing(MechInd, PathEndMode.Touch);

            Toil waitToil = Toils_General.Wait(ResupplyDuration)
                .FailOnDespawnedNullOrForbidden(MechInd)
                .FailOnCannotTouch(MechInd, PathEndMode.Touch)
                .WithProgressBarToilDelay(MechInd)
                .WithEffect(EffecterDefOf.MechRepairing, MechInd);
            waitToil.PlaySustainerOrSound(SoundDefOf.RepairMech_Touch);
            waitToil.AddPreInitAction(delegate
            {
                // 派发机兵等待整备作业
                Job waitJob = JobMaker.MakeJob(USAC_DefOf.USAC_WaitForResupply);
                Mech.jobs.StartJob(waitJob, JobCondition.InterruptForced);
            });
            waitToil.AddFinishAction(delegate
            {
                // 打断机兵等待作业
                if (Mech.CurJobDef == USAC_DefOf.USAC_WaitForResupply)
                    Mech.jobs.EndCurrentJob(JobCondition.InterruptForced);
            });

            yield return waitToil;

            yield return Toils_General.Do(delegate
            {
                Readiness.Resupply(Supply);
            });
        }

        // 计算实际消耗零件数
        private int GetRequiredSupplyCount()
        {
            float needed = Readiness.Props.capacity - Readiness.Readiness;
            float restorePerItem = Readiness.Props.capacity * 0.25f;
            int n = UnityEngine.Mathf.CeilToInt(needed / restorePerItem);
            float waste = n * restorePerItem - needed;
            // 浪费超过5%则少取一个零件
            if (waste > Readiness.Props.capacity * 0.05f && n > 1) n--;
            return n;
        }
    }
}
