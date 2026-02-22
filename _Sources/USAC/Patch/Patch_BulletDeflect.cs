using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace USAC
{
    // 记录子弹命中前的信息
    static class DeflectContext
    {
        [ThreadStatic] public static bool active;
        [ThreadStatic] public static ThingDef bulletDef;
        [ThreadStatic] public static Vector3 position;
        [ThreadStatic] public static Vector3 origin;
        [ThreadStatic] public static Map map;
        [ThreadStatic] public static bool isHighY;

        // 高Y入射子弹
        public static readonly HashSet<int> HighYBullets = new HashSet<int>();

        // 高Y弹飞子弹
        public static readonly HashSet<int> HighYDeflected =
            new HashSet<int>();

        // 高于Pawn的渲染高度
        public static readonly float HighAlt =
            AltitudeLayer.Pawn.AltitudeFor() + Altitudes.AltInc * 2;

        public static void Clear()
        {
            active = false;
            bulletDef = null;
            map = null;
            isHighY = false;
        }
    }

    // 子弹发射时标记高Y
    [HarmonyPatch(typeof(Projectile), nameof(Projectile.Launch),
        typeof(Thing), typeof(Vector3), typeof(LocalTargetInfo),
        typeof(LocalTargetInfo), typeof(ProjectileHitFlags),
        typeof(bool), typeof(Thing), typeof(ThingDef))]
    static class Patch_BulletDeflect_MarkHighY
    {
        [HarmonyPostfix]
        static void Postfix(
            Projectile __instance,
            LocalTargetInfo intendedTarget)
        {
            try
            {
                if (!(__instance is Bullet)) return;
                if (!(intendedTarget.Thing is Pawn pawn)) return;
                if (pawn.TryGetComp<CompBulletDeflect>() == null) return;

                Rand.PushState(__instance.thingIDNumber);
                bool highY = Rand.Chance(0.5f);
                Rand.PopState();

                if (highY)
                    DeflectContext.HighYBullets.Add(
                        __instance.thingIDNumber);
            }
            catch (Exception e)
            {
                Log.Error($"[USAC] MarkHighY: {e}");
            }
        }
    }

    // 抬高高Y子弹的渲染层级
    [HarmonyPatch(typeof(Projectile), "DrawAt")]
    static class Patch_BulletDeflect_DrawHighY
    {
        [HarmonyPrefix]
        static void Prefix(
            Projectile __instance, ref Vector3 drawLoc)
        {
            if (DeflectContext.HighYBullets.Count == 0 &&
                DeflectContext.HighYDeflected.Count == 0)
                return;

            int id = __instance.thingIDNumber;
            if (DeflectContext.HighYBullets.Contains(id) ||
                DeflectContext.HighYDeflected.Contains(id))
            {
                // 已销毁则清理残留
                if (!__instance.Spawned)
                {
                    DeflectContext.HighYBullets.Remove(id);
                    DeflectContext.HighYDeflected.Remove(id);
                    return;
                }
                drawLoc.y = DeflectContext.HighAlt;
            }
        }
    }

    // Prefix: 子弹命中前记录信息并清理标记
    [HarmonyPatch(typeof(Bullet), "Impact")]
    static class Patch_BulletDeflect_RecordInfo
    {
        static readonly FieldInfo OriginField =
            typeof(Projectile).GetField("origin",
                BindingFlags.Instance | BindingFlags.NonPublic);

        [HarmonyPrefix]
        static void Prefix(Bullet __instance, Thing hitThing)
        {
            int id = __instance.thingIDNumber;
            // 先记录高Y状态再清理
            bool wasHighY = DeflectContext.HighYBullets.Remove(id);
            DeflectContext.HighYDeflected.Remove(id);

            DeflectContext.Clear();
            if (hitThing is Pawn pawn)
            {
                var comp = pawn.TryGetComp<CompBulletDeflect>();
                if (comp != null)
                {
                    DeflectContext.active = true;
                    DeflectContext.bulletDef = __instance.def;
                    DeflectContext.position = __instance.ExactPosition;
                    DeflectContext.origin =
                        (Vector3)OriginField.GetValue(__instance);
                    DeflectContext.map = __instance.Map;
                    DeflectContext.isHighY = wasHighY;
                }
            }
        }
    }

    // Postfix: 原版护甲判定deflect后生成弹飞子弹
    [HarmonyPatch(typeof(DamageWorker_AddInjury), "ApplyToPawn")]
    static class Patch_BulletDeflect_SpawnDeflected
    {
        [HarmonyPostfix]
        static void Postfix(
            DamageWorker.DamageResult __result,
            Pawn pawn)
        {
            try
            {
                if (!DeflectContext.active) return;
                if (!__result.deflected) return;
                if (!__result.deflectedByMetalArmor) return;

                var comp = pawn.TryGetComp<CompBulletDeflect>();
                if (comp == null) return;

                SpawnDeflectedBullet(comp, pawn);
            }
            catch (Exception e)
            {
                Log.Error($"[USAC] BulletDeflect: {e}");
            }
            finally
            {
                DeflectContext.Clear();
            }
        }

        static void SpawnDeflectedBullet(
            CompBulletDeflect comp, Pawn pawn)
        {
            var ctx = DeflectContext.bulletDef;
            var map = DeflectContext.map;
            if (ctx == null || map == null) return;

            var hitPos = DeflectContext.position;
            var origin = DeflectContext.origin;
            var props = comp.Props;
            bool isHighY = DeflectContext.isHighY;

            // 入射方向
            Vector3 incoming = (hitPos - origin).Yto0().normalized;
            if (incoming == Vector3.zero) incoming = Vector3.forward;

            Vector3 deflected;
            if (isHighY)
            {
                // 高Y掠射: 继续入射方向偏转±45°
                float jitter = Rand.Range(-45f, 45f);
                deflected =
                    Quaternion.Euler(0f, jitter, 0f) * incoming;
            }
            else
            {
                // 普通反射: 四面装甲板法线
                Vector3 negIn = -incoming;
                Vector3 normal;
                if (Mathf.Abs(negIn.x) > Mathf.Abs(negIn.z))
                    normal = new Vector3(
                        Mathf.Sign(negIn.x), 0f, 0f);
                else
                    normal = new Vector3(
                        0f, 0f, Mathf.Sign(negIn.z));

                deflected = incoming
                    - 2f * Vector3.Dot(incoming, normal) * normal;
                deflected = deflected.Yto0().normalized;

                float jitterAngle = Rand.Range(
                    -props.normalJitter, props.normalJitter) * 90f;
                deflected =
                    Quaternion.Euler(0f, jitterAngle, 0f) * deflected;
            }

            // 弹飞终点
            Vector3 dest = hitPos + deflected * props.deflectFlightDist;
            IntVec3 destCell = dest.ToIntVec3();
            if (!destCell.InBounds(map)) return;

            // 生成新子弹
            IntVec3 spawnCell = hitPos.ToIntVec3();
            if (!spawnCell.InBounds(map)) return;

            var bullet = (Projectile)ThingMaker.MakeThing(ctx);
            bullet.HitFlags = ProjectileHitFlags.None;
            GenSpawn.Spawn(bullet, spawnCell, map);
            bullet.Launch(
                pawn, hitPos,
                new LocalTargetInfo(destCell),
                new LocalTargetInfo(destCell),
                ProjectileHitFlags.None);

            // 标记高Y弹飞子弹
            if (isHighY)
                DeflectContext.HighYDeflected.Add(
                    bullet.thingIDNumber);

            // 碰撞火花
            if (hitPos.ShouldSpawnMotesAt(map))
            {
                FleckMaker.ThrowMicroSparks(hitPos, map);
                FleckMaker.ThrowMicroSparks(hitPos, map);
            }
        }
    }
}
