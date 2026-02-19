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
        private int noEnemyTicks;

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
            // 校验区域内是否存在敌对目标
            bool hasEnemy = false;

            if (lord.LordJob is LordJob_MiningGuard miningGuard)
            {
                foreach (Pawn guard in lord.ownedPawns)
                {
                    if (guard.Dead || guard.Downed || !guard.Spawned) continue;

                    // 检查追击范围内是否有敌对目标
                    foreach (Faction hostileFaction in miningGuard.HostileFactions)
                    {
                        foreach (Pawn enemy in lord.Map.mapPawns.SpawnedPawnsInFaction(hostileFaction))
                        {
                            if (enemy.Dead || enemy.Downed) continue;

                            if (enemy.Position.InHorDistOf(defendPoint, maxChaseRadius))
                            {
                                hasEnemy = true;
                                break;
                            }
                        }
                        if (hasEnemy) break;
                    }
                    if (hasEnemy) break;
                }
            }

            if (hasEnemy)
            {
                noEnemyTicks = 0;
            }
            else
            {
                noEnemyTicks++;

                // 无敌人一段时间后返回防守
                if (noEnemyTicks >= ReturnToDefendDelay)
                {
                    lord.ReceiveMemo("ThreatsCleared");
                }
            }
        }

        public override void Notify_PawnDamaged(Pawn victim, DamageInfo dinfo)
        {
            base.Notify_PawnDamaged(victim, dinfo);

            // 获取攻击者的攻击者实例
            Thing instigator = dinfo.Instigator;
            if (instigator == null) return;

            Faction attackerFaction = instigator.Faction;

            // 不对自己派系敌对敌对
            if (attackerFaction != null && attackerFaction == lord.faction) return;

            // 将攻击者派系加入敌对列表
            if (attackerFaction != null && lord.LordJob is LordJob_MiningGuard miningGuard)
            {
                miningGuard.AddHostileFaction(attackerFaction);
            }

            // 更新攻击目标缓存标缓存
            Map map = lord.Map;
            if (map != null)
            {
                foreach (Pawn guard in lord.ownedPawns)
                {
                    if (guard.Spawned)
                    {
                        map.attackTargetsCache.UpdateTarget(guard);
                    }
                }

                if (instigator is Pawn attackerPawn && attackerPawn.Spawned)
                {
                    map.attackTargetsCache.UpdateTarget(attackerPawn);
                }
            }
        }

        #endregion
    }
}
