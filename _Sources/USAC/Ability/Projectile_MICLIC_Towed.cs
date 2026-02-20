using RimWorld;
using Verse;
using UnityEngine;
using System.Collections.Generic;
using Verse.AI;

namespace USAC
{
    // 基于解析解的弹道计算
    // 抛弃迭代使用解析解
    [StaticConstructorOnStartup]
    public class Projectile_MICLIC_Towed : Projectile_Explosive
    {
        private const int TOTAL_NODES = 40;
        private const int CHARGE_START = 20;
        private const float LAUNCHER_HEIGHT = 0.55f;

        // 物理常数
        private const float GRAVITY = 0.0055f;
        private const float BURNOUT_PROG = 0.15f;

        private VerletRope rope;
        private bool isLanded = false;
        private int landingTick = -1;

        // 缓存发射时的平面坐标
        private Vector2 cachedLauncherPlanePos;
        private bool launcherPosCached = false;

        // 动态计算出的仰角
        private float launchAngleParams_Sin;
        private float launchAngleParams_Cos;

        private Vector3 rocketPhysPos;
        private Vector3 rocketPhysVel;

        public override Vector3 ExactPosition => new Vector3(rocketPhysPos.x, origin.y, rocketPhysPos.y);

        private static readonly Material CableMat =
            MaterialPool.MatFrom(BaseContent.WhiteTex,
                ShaderDatabase.Transparent, new Color(0.1f, 0.1f, 0.1f));

        private static readonly Material RocketEmptyMat = MaterialPool.MatFrom("Things/Projectile/lineCharge_RocketEmpty", ShaderDatabase.Cutout);
        private static readonly Material RocketLandedMat = MaterialPool.MatFrom("Things/Projectile/lineCharge_RocketLanded", ShaderDatabase.Cutout);

        private void EnsureRopeInit()
        {
            if (rope != null) return;

            float totalDist = (destination - origin).MagnitudeHorizontal();
            float totalLen = totalDist * 1.28f;
            rope = new VerletRope(TOTAL_NODES, totalLen, GRAVITY, 40);

            for (int i = 0; i < TOTAL_NODES; i++)
                rope.Nodes[i].mass = (i >= CHARGE_START) ? 5.0f : 0.1f;

            launchAngleParams_Sin = 0; // 占位符将被下方强化求解器覆盖
            launchAngleParams_Cos = 0;

            // 基于发射矢量计算锚点
            Vector2 launchDirFlat = new Vector2(destination.x - origin.x, destination.z - origin.z).normalized;
            Vector3 startPos = origin + (new Vector3(launchDirFlat.x, 0, launchDirFlat.y) * -0.9f);
            cachedLauncherPlanePos = new Vector2(startPos.x, startPos.z);
            launcherPosCached = true;

            rope.InitFlaked(cachedLauncherPlanePos, LAUNCHER_HEIGHT, launchDirFlat);

            // 初始化物理状态
            rocketPhysPos = new Vector3(cachedLauncherPlanePos.x, cachedLauncherPlanePos.y, LAUNCHER_HEIGHT);

            // 带高度补偿的物理求解
            float v = def.projectile.SpeedTilesPerTick;
            float g = GRAVITY;
            float h = LAUNCHER_HEIGHT;
            // 真实需要跨越的水平距离
            float R = (new Vector2(destination.x, destination.z) - cachedLauncherPlanePos).magnitude;

            // 补偿欧拉积分误差
            R *= 1.015f;

            float v2 = v * v;
            float gR = g * R;
            float discriminant = v2 * v2 - g * (g * R * R + 2 * h * v2);

            float finalAngle;
            if (discriminant < 0)
            {
                // 射程不足使用45度
                finalAngle = 45f * Mathf.Deg2Rad;
            }
            else
            {
                // 求高抛角解 (取正号)
                float tanTheta = (v2 + Mathf.Sqrt(discriminant)) / gR;
                finalAngle = Mathf.Atan(tanTheta);
            }

            launchAngleParams_Sin = Mathf.Sin(finalAngle);
            launchAngleParams_Cos = Mathf.Cos(finalAngle);

            float speed = def.projectile.SpeedTilesPerTick;
            rocketPhysVel = new Vector3(launchDirFlat.x * speed * launchAngleParams_Cos,
                                        launchDirFlat.y * speed * launchAngleParams_Cos,
                                        speed * launchAngleParams_Sin);
        }

        protected override void Tick()
        {
            base.Tick();
            if (!this.Spawned) return;

            EnsureRopeInit();

            Vector2 startPlane = cachedLauncherPlanePos;
            Vector3 currentRocketPos;

            // 推进阶段检查
            float totalDist = (destination - origin).MagnitudeHorizontal();
            float currentDist = 0f;

            if (isLanded)
            {
                currentRocketPos = rocketPhysPos;
                if (Find.TickManager.TicksGame >= landingTick + 45) SyncExplodeAll();
            }
            else
            {
                SimulateRocketPhysics();
                currentRocketPos = rocketPhysPos;

                currentDist = Vector2.Distance(new Vector2(origin.x, origin.z), new Vector2(rocketPhysPos.x, rocketPhysPos.y));

                // 仅在助推阶段喷射尾迹
                if (totalDist > 0.001f && currentDist / totalDist <= BURNOUT_PROG)
                {
                    ThrowExhaust(rocketPhysPos, rocketPhysVel);
                }

                if (rocketPhysPos.z <= 0f)
                {
                    rocketPhysPos.z = 0f;
                    OnPhysicsLanded();
                }
            }

            // 同步格子坐标
            this.Position = ExactPosition.ToIntVec3();

            rope.Simulate(startPlane, LAUNCHER_HEIGHT, new Vector2(currentRocketPos.x, currentRocketPos.y), currentRocketPos.z);
        }

        private void SimulateRocketPhysics()
        {
            rocketPhysVel.z -= GRAVITY;
            rocketPhysPos += rocketPhysVel;
        }

        private void ThrowExhaust(Vector3 rPos, Vector3 rVel)
        {
            if (this.Map == null) return;

            Vector3 visualPos = new Vector3(rPos.x, 0, rPos.y + rPos.z);
            float speed = rVel.magnitude;
            Vector3 retroVel = -rVel.normalized * (speed * 0.2f);

            for (int i = 0; i < 3; i++)
            {
                float time = (Find.TickManager.TicksGame + i * 0.33f) * 0.8f;
                Vector3 cross = Vector3.Cross(rVel, Vector3.up).normalized;
                if (cross == Vector3.zero) cross = Vector3.right;
                Vector3 up = Vector3.Cross(cross, rVel).normalized;
                float radius = 0.3f + Mathf.Sin(time * 0.5f) * 0.1f;
                Vector3 spiralOffset = (cross * Mathf.Cos(time) + up * Mathf.Sin(time)) * radius;
                Vector3 smokePos = rPos + spiralOffset * 0.5f;
                Vector3 finalPos = new Vector3(smokePos.x, 0, smokePos.y + smokePos.z);
                FleckDef fleckDef = (i % 2 == 0) ? FleckDefOf.Smoke : FleckDefOf.DustPuffThick;
                FleckCreationData data = FleckMaker.GetDataStatic(finalPos, Map, fleckDef, 2.5f);
                data.rotation = Rand.Range(0, 360);
                data.rotationRate = Rand.Range(-30f, 30f);
                data.velocityAngle = (retroVel + spiralOffset * 0.15f).AngleFlat();
                data.velocitySpeed = 0.6f + Rand.Range(0f, 0.5f);
                data.solidTimeOverride = 5.0f + Rand.Range(0f, 1.5f);
                data.scale = 2.4f + Rand.Range(0f, 1.2f);
                float gray = Rand.Range(0.3f, 0.5f);
                data.instanceColor = new Color(gray, gray, gray, 0.95f);

                Map.flecks.CreateFleck(data);
            }

            if (speed > 0.5f)
            {
                FleckCreationData fire = FleckMaker.GetDataStatic(visualPos, Map, FleckDefOf.MicroSparks, 0.6f);
                fire.instanceColor = new Color(1f, 0.8f, 0.2f, 0.9f);
                fire.velocitySpeed = 0.1f;
                fire.solidTimeOverride = 0.2f;
                Map.flecks.CreateFleck(fire);
            }
        }

        private void OnPhysicsLanded()
        {
            if (isLanded) return;
            isLanded = true;
            landingTick = Find.TickManager.TicksGame;
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (rope == null) return;
            // 修正渲染层级顺序
            float baseLayer = AltitudeLayer.MoteOverhead.AltitudeFor();
            float layerLine = baseLayer - 0.05f;
            float layerCharge = baseLayer;
            float layerRocket = baseLayer + 0.5f;

            Vector2 lPlane = cachedLauncherPlanePos;

            Vector3 rPos = rocketPhysPos;
            Vector3 rVis = new Vector3(rPos.x, layerRocket, rPos.y + rPos.z);

            float angle;
            // 按视觉速度矢量旋转
            if (!isLanded && rocketPhysVel.MagnitudeHorizontal() > 0.001f)
                angle = Mathf.Atan2(rocketPhysVel.x, rocketPhysVel.y + rocketPhysVel.z) * Mathf.Rad2Deg;
            else
                angle = origin.AngleToFlat(destination);

            Material rocketMat = def.graphic.MatSingle;
            if (isLanded)
            {
                rocketMat = RocketLandedMat;
            }
            else
            {
                float totalDist = (destination - origin).MagnitudeHorizontal();
                float currentDist = Vector2.Distance(new Vector2(origin.x, origin.z), new Vector2(rocketPhysPos.x, rocketPhysPos.y));
                if (totalDist > 0.001f && currentDist / totalDist > BURNOUT_PROG)
                    rocketMat = RocketEmptyMat;
            }

            Vector2 drawSize = def.graphicData.drawSize;
            Matrix4x4 rocketMatrix = Matrix4x4.TRS(rVis, Quaternion.AngleAxis(angle, Vector3.up), new Vector3(drawSize.x, 1f, drawSize.y));
            Graphics.DrawMesh(MeshPool.plane10, rocketMatrix, rocketMat, 0);

            // 反向查找首个出舱节点
            int startDrawIndex = 0;
            for (int i = rope.NodeCount - 1; i >= 0; i--)
            {
                if (Vector2.Distance(rope.Nodes[i].planePos, lPlane) <= 0.6f)
                {
                    startDrawIndex = i + 1;
                    break;
                }
            }

            List<Vector3> activeDots = new List<Vector3>();
            for (int i = startDrawIndex; i < rope.NodeCount; i++)
            {
                // Y轴折叠模拟
                float foldOffset = (i % 3) * 0.005f + Mathf.Sin(i * 0.5f) * 0.005f;
                activeDots.Add(rope.GetVisualPos(i, layerLine + foldOffset));
            }

            // 无论如何都要画线作为保底连接
            if (activeDots.Count > 1) DrawSmoothCable(activeDots, 0.11f);

            // 绘制炸药包段
            Graphic segGfx = USAC_DefOf.USAC_MICLIC_Segment?.graphic;
            if (segGfx != null)
            {
                Material mat = segGfx.MatSingle;
                Vector2 size = segGfx.drawSize;
                Mesh mesh = MeshPool.plane10;

                int startSegment = Mathf.Max(CHARGE_START, startDrawIndex);

                for (int i = startSegment; i < rope.NodeCount - 1; i++)
                {
                    Vector3 a = rope.GetVisualPos(i, layerCharge);
                    Vector3 b = rope.GetVisualPos(i + 1, layerCharge);

                    // 计算中点和方向
                    Vector3 mid = (a + b) * 0.5f;
                    Vector3 dir = b - a;
                    float len = dir.magnitude;

                    if (len < 0.001f) continue;

                    Quaternion rot = Quaternion.LookRotation(dir);

                    // 偏移Y轴防重叠
                    float foldOffset = (i % 5) * 0.002f;
                    mid.y += foldOffset;

                    // 使用定义尺寸固定缩放
                    // 允许物理重叠表现
                    Vector3 scale = new Vector3(size.x, 1f, size.y);

                    Matrix4x4 matrix = Matrix4x4.TRS(mid, rot, scale);
                    Graphics.DrawMesh(mesh, matrix, mat, 0);
                }
            }
        }

        private void DrawSmoothCable(List<Vector3> points, float width)
        {
            int segs = 3;
            if (points.Count == 0) return;
            Vector3 lastP = points[0];
            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector3 p0 = (i == 0) ? points[i] : points[i - 1];
                Vector3 p1 = points[i];
                Vector3 p2 = points[i + 1];
                Vector3 p3 = (i + 2 < points.Count) ? points[i + 2] : points[i + 1];
                for (int j = 1; j <= segs; j++)
                {
                    float t = j / (float)segs;
                    float t2 = t * t; float t3 = t2 * t;
                    Vector3 curP = 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
                    GenDraw.DrawLineBetween(lastP, curP, CableMat, width);
                    lastP = curP;
                }
            }
        }

        private Vector2 GetLauncherPlanePos() => cachedLauncherPlanePos;

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            if (isLanded) return;
            // 此时忽略碰撞
        }

        private void SyncExplodeAll()
        {
            if (rope == null) { this.Destroy(); return; }
            float angle = origin.AngleToFlat(destination);
            for (int i = CHARGE_START; i < rope.NodeCount; i++)
            {
                Vector3 nodePos = new Vector3(rope.Nodes[i].planePos.x, 0, rope.Nodes[i].planePos.y);
                IntVec3 cell = nodePos.ToIntVec3();
                if (Map != null && cell.InBounds(Map))
                    GenExplosion.DoExplosion(cell, Map, def.projectile.explosionRadius, DamageDefOf.Bomb, launcher, direction: angle);
            }
            this.Destroy();
        }

        public override void Launch(Thing launcher, Vector3 origin, LocalTargetInfo usedTarget, LocalTargetInfo intendedTarget, ProjectileHitFlags hitFlags, bool preventFriendlyFire = false, Thing equipment = null, ThingDef targetCoverDef = null)
        {
            base.Launch(launcher, origin, usedTarget, intendedTarget, hitFlags, preventFriendlyFire, equipment, targetCoverDef);
            // 设置超长寿命防止销毁
            this.ticksToImpact = 9999;

            // 强制从属发射者进入在此等待状态
            if (launcher is Pawn p && !p.Dead && p.Map != null)
            {
                Job job = JobMaker.MakeJob(USAC_DefOf.USAC_WaitDetonate, this);
                job.playerForced = true;
                p.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref isLanded, "isLanded", false);
            Scribe_Values.Look(ref landingTick, "landingTick", -1);
            Scribe_Values.Look(ref rocketPhysPos, "rocketPhysPos");
            Scribe_Values.Look(ref rocketPhysVel, "rocketPhysVel");
            Scribe_Values.Look(ref cachedLauncherPlanePos, "cachedLauncherPlanePos");
            Scribe_Values.Look(ref launcherPosCached, "launcherPosCached", false);
        }
    }
}
