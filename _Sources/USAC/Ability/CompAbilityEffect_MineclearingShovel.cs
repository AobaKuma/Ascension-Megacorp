using RimWorld;
using Verse;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace USAC
{
    // 定义排雷铲技能组件类
    // 冲刺落地释放扇形起爆
    public class CompAbilityEffect_MineclearingShovel : CompAbilityEffect, ICompAbilityEffectOnJumpCompleted
    {
        public void OnJumpCompleted(IntVec3 origin, LocalTargetInfo target)
        {
            Pawn pawn = parent.pawn;
            if (pawn == null || !pawn.Spawned) return;

            IntVec3 landPos = pawn.Position;
            // 计算冲刺向量角度
            float angle = (landPos != origin)
                ? (landPos.ToVector3Shifted() - origin.ToVector3Shifted()).AngleFlat()
                : pawn.Rotation.AsAngle;

            // 执行两次完全相同的扇形起爆
            // 用于叠加伤害破拆目标
            DoShovelExplosion(landPos, angle, 6.9f);
            DoShovelExplosion(landPos, angle, 6.9f);
        }

        private void DoShovelExplosion(IntVec3 center, float angle, float radius)
        {
            Map map = parent.pawn.Map;
            if (!center.InBounds(map)) return;

            // 获取标准对称扇形区域
            List<IntVec3> cells = GetFanCells(center, angle, radius);

            GenExplosion.DoExplosion(
                center: center,
                map: map,
                radius: radius,
                damType: DamageDefOf.Bomb,
                instigator: parent.pawn,
                damAmount: 50,
                armorPenetration: 0.1f,
                explosionSound: SoundDef.Named("Explosion_Bomb"),
                applyDamageToExplosionCellsNeighbors: false,
                direction: angle,
                ignoredThings: new List<Thing> { parent.pawn },
                overrideCells: cells
            );
        }

        private List<IntVec3> GetFanCells(IntVec3 center, float angle, float radius)
        {
            // 获取受阻挡的爆炸格位
            var damageWorker = DamageDefOf.Bomb.Worker;
            var allCells = damageWorker.ExplosionCellsToHit(center, parent.pawn.Map, radius, null, null, null);

            List<IntVec3> filtered = new List<IntVec3>();
            Vector3 centerPos = center.ToVector3Shifted();

            foreach (var cell in allCells)
            {
                if (cell == center) continue;

                // 进行扇形切片
                Vector3 cellPos = cell.ToVector3Shifted();
                float cellAngle = (cellPos - centerPos).AngleFlat();

                if (Mathf.Abs(Mathf.DeltaAngle(angle, cellAngle)) <= 45.1f)
                {
                    filtered.Add(cell);
                }
            }
            return filtered;
        }
    }

    public class CompProperties_MineclearingShovel : CompProperties_AbilityEffect
    {
        public CompProperties_MineclearingShovel()
        {
            compClass = typeof(CompAbilityEffect_MineclearingShovel);
        }
    }
}
