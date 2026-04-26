using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace USAC
{
    // 定义机兵区域威胁清理任务
    // 追猎消灭敌人后返回防守态
    public class LordToil_KillThreats : LordToil
    {
        #region 字段

        private IntVec3 defendPoint;
        private float maxChaseRadius;
        private int noEnemySinceTick;

        // 无敌人多久后返回防守长
        private const int ReturnToDefendDelay = 300;

        #endregion

        #region 属性

        public override bool AllowSatisfyLongNeeds => false;

        #endregion

        #region 构造函数

        public LordToil_KillThreats(IntVec3 defendPoint, float maxChaseRadius = 30f)
        {
            this.defendPoint = defendPoint;
            this.maxChaseRadius = maxChaseRadius;
        }

        #endregion

        #region 公共方法

        public override void UpdateAllDuties()
        {
            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                Pawn pawn = lord.ownedPawns[i];
                if (pawn?.mindState == null) continue;

                // 分配攻击职责
                pawn.mindState.duty = new PawnDuty(DutyDefOf.AssaultColony);
            }
        }

        public override void LordToilTick()
        {
            // 每15tick检查一次
            if (Find.TickManager.TicksGame % 15 != 0) return;

            bool hasEnemy = false;

            // 直接查引擎维护的敌对目标缓存
            var hostileTargets = lord.Map.attackTargetsCache
                .TargetsHostileToFaction(lord.faction);

            foreach (var target in hostileTargets)
            {
                if (target is not Pawn enemy) continue;
                if (enemy.Dead || enemy.Downed || !enemy.Spawned) continue;
                if (enemy.Position.InHorDistOf(defendPoint, maxChaseRadius))
                {
                    hasEnemy = true;
                    break;
                }
            }

            if (hasEnemy)
            {
                noEnemySinceTick = Find.TickManager.TicksGame;
            }
            else
            {
                if (noEnemySinceTick > 0 && Find.TickManager.TicksGame - noEnemySinceTick >= ReturnToDefendDelay)
                    lord.ReceiveMemo("ThreatsCleared");
            }
        }

        #endregion
    }
}
