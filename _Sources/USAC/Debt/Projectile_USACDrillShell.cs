using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace USAC
{
    // 厚岩顶破拆钻地弹
    public class Projectile_USACDrillShell : Projectile
    {
        #region 字段
        private bool impactsHandled = false;
        private Thing payloadTarget;
        #endregion

        #region 生命周期
        public void SetPayload(Thing target)
        {
            payloadTarget = target;
        }

        protected override void ImpactSomething()
        {
            // 强制触发自定义命中逻辑
            Impact(null);
        }

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            if (impactsHandled) return;
            impactsHandled = true;

            Map map = Map;
            IntVec3 pos = Position;

            // 破拆目标格屋顶
            BreakRoofSafely(payloadTarget, map);

            // 触发视觉与屏幕颤抖
            float radius = Mathf.Max(payloadTarget?.def.size.x ?? 1f, payloadTarget?.def.size.z ?? 1f) / 2f + 1.5f;

            if (map == Find.CurrentMap)
            {
                // 计算相机抖动强度
                float magnitude = (pos.ToVector3Shifted() - Find.Camera.transform.position).magnitude;
                Find.CameraDriver.shaker.DoShake(4f * radius * 2f / magnitude);
            }

            // 产生爆炸视觉效果
            FleckMaker.Static(pos, map, FleckDefOf.ExplosionFlash, radius * 4f);
            FleckMaker.ThrowLightningGlow(pos.ToVector3Shifted(), map, 3.5f);

            // 渲染爆炸尘埃
            if (payloadTarget != null)
            {
                foreach (IntVec3 cell in GenRadial.RadialCellsAround(payloadTarget.Position, radius, true))
                {
                    if (Rand.Chance(0.5f))
                        FleckMaker.ThrowDustPuffThick(cell.ToVector3Shifted(), map, Rand.Range(1.5f, 2.5f), Color.white);
                    else if (Rand.Chance(0.4f))
                        FleckMaker.ThrowDustPuff(cell.ToVector3Shifted() + Gen.RandomHorizontalVector(0.5f), map, Rand.Range(1.2f, 2f));
                }
            }
            else
            {
                FleckMaker.ThrowDustPuffThick(pos.ToVector3Shifted(), map, 2.0f, Color.white);
                for (int i = 0; i < 3; i++)
                    FleckMaker.ThrowDustPuff(pos.ToVector3Shifted() + Gen.RandomHorizontalVector(0.5f), map, 1.5f);
            }

            if (def.projectile.soundExplode != null)
                def.projectile.soundExplode.PlayOneShot(SoundInfo.InMap(new TargetInfo(pos, map)));

            // 生成后续轨道夹具
            if (payloadTarget is { Spawned: true })
                SpawnFollowupGripper(payloadTarget, map);

            Destroy();
        }
        #endregion

        #region 私有方法
        private void BreakRoofSafely(Thing target, Map map)
        {
            if (target == null || map == null) return;

            // 确定圆形破拆半径
            float radius = Mathf.Max(target.def.size.x, target.def.size.z) / 2f + 1.5f;

            foreach (IntVec3 pos in GenRadial.RadialCellsAround(target.Position, radius, true))
            {
                if (!pos.InBounds(map) || !pos.Roofed(map)) continue;

                var roof = pos.GetRoof(map);
                if (roof != null && roof.isThickRoof)
                {
                    // 强制设为薄岩顶以防塌方
                    map.roofGrid.SetRoof(pos, RoofDefOf.RoofRockThin);
                }

                map.roofGrid.SetRoof(pos, null);

                // 移除当前帧生成的碎石
                var rocks = pos.GetFirstThing(map, ThingDefOf.CollapsedRocks);
                if (rocks != null) rocks.Destroy(DestroyMode.Vanish);
            }
        }

        private void SpawnFollowupGripper(Thing target, Map map)
        {
            var gripper = (Skyfaller_USACGripper)ThingMaker.MakeThing(USAC_DefOf.USAC_GripperIncoming);
            gripper.SetTarget(target);
            GenSpawn.Spawn(gripper, target.Position, map);
        }
        #endregion

        #region 数据持久化
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref payloadTarget, "payloadTarget");
            Scribe_Values.Look(ref impactsHandled, "impactsHandled");
        }
        #endregion
    }
}
