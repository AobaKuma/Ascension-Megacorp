using System.Collections.Generic;
using Verse;
using Verse.AI;
using RimWorld;

namespace USAC
{
    // 等待排雷索引爆的战斗任务
    // 强制执行不可打断
    public class JobDriver_WaitDetonate : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Toil wait = ToilMaker.MakeToil("WaitDetonate");
            wait.tickAction = () =>
            {
                Projectile proj = job.targetA.Thing as Projectile;
                if (proj == null || proj.Destroyed)
                {
                    EndJobWith(JobCondition.Succeeded);
                    return;
                }
                
                // 始终注视目标方向
                pawn.rotationTracker.FaceTarget(proj);
            };
            wait.defaultCompleteMode = ToilCompleteMode.Never;
            yield return wait;
        }
    }
}
