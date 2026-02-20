using RimWorld;
using Verse;
using UnityEngine;
using System.Collections.Generic;


namespace USAC
{
    // 脚本承技能
    public class CompAbilityEffect_MICLIC : CompAbilityEffect
    {
        private new CompProperties_AbilityMICLIC Props => (CompProperties_AbilityMICLIC)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn pawn = parent.pawn;
            if (pawn == null || !pawn.Spawned) return;

            // 实体化火箭
            Projectile projectile = (Projectile)GenSpawn.Spawn(Props.projectileDef, pawn.Position, pawn.Map);
            projectile.Launch(pawn, pawn.DrawPos, target, target, ProjectileHitFlags.All);
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            if (target.IsValid && parent.pawn.Map != null)
            {
                Vector3 start = parent.pawn.DrawPos;
                Vector3 end = target.CenterVector3;
                float totalDist = (end - start).MagnitudeHorizontal();
                float shotAngle = start.AngleToFlat(end);

                int chargeStart = 20;
                int totalSegments = 40;

                // 向量指方向
                Vector3 launchDir = (end - start).normalized;

                // 索道隐虚空
                // 空段不引爆
                // 落位即锚点
                float chargeDist = totalDist * 0.7f; // 长度乘系数

                // 目标即终点
                Vector3 chargeStartPos = end - launchDir * chargeDist;
                // 极近则钳制
                if (totalDist < chargeDist)
                {
                    chargeStartPos = start;
                    chargeDist = totalDist;
                }

                float layerCharge = AltitudeLayer.MoteOverhead.AltitudeFor();

                // 白线连始终
                GenDraw.DrawLineBetween(start, end, SimpleColor.White);

                // 半径展长廊
                float radius = Props.projectileDef.projectile.explosionRadius;
                if (radius <= 0f) radius = 3.9f;
                HashSet<IntVec3> explosionCells = new HashSet<IntVec3>();

                Graphic segGfx = USAC_DefOf.USAC_MICLIC_Segment?.graphic;
                Material previewMat = null;
                Vector2 size = Vector2.one;
                Mesh mesh = MeshPool.plane10;

                if (segGfx != null)
                {
                    Material protoMat = segGfx.MatSingle;
                    previewMat = MaterialPool.MatFrom(new MaterialRequest
                    {
                        mainTex = protoMat.mainTexture,
                        shader = ShaderDatabase.Transparent,
                        color = new Color(1f, 1f, 1f, 0.35f)
                    });
                    size = segGfx.drawSize;
                }

                int explodeNodeCount = totalSegments - chargeStart;
                float segmentLen = chargeDist / Mathf.Max(1, explodeNodeCount - 1);

                for (int i = 0; i < explodeNodeCount; i++)
                {
                    Vector3 currentPos = chargeStartPos + launchDir * (segmentLen * i);
                    IntVec3 cell = currentPos.ToIntVec3();

                    // 坐标纳合集
                    int rCeil = Mathf.CeilToInt(radius);
                    for (int dx = -rCeil; dx <= rCeil; dx++)
                    {
                        for (int dz = -rCeil; dz <= rCeil; dz++)
                        {
                            IntVec3 c = new IntVec3(cell.x + dx, 0, cell.z + dz);
                            if (c.InBounds(parent.pawn.Map) && c.DistanceTo(cell) <= radius)
                            {
                                explosionCells.Add(c);
                            }
                        }
                    }

                    if (segGfx != null && i < explodeNodeCount - 1)
                    {
                        Vector3 nextPos = chargeStartPos + launchDir * (segmentLen * (i + 1));
                        Vector3 mid = (currentPos + nextPos) * 0.5f;
                        mid.y = layerCharge; // 虚影贴地表

                        Quaternion rot = Quaternion.LookRotation(launchDir);
                        Matrix4x4 matrix = Matrix4x4.TRS(mid, rot, new Vector3(size.x, 1f, size.y));
                        Graphics.DrawMesh(mesh, matrix, previewMat, 0);
                    }
                }

                // 边沿绘白线
                List<IntVec3> cellList = new List<IntVec3>(explosionCells);
                GenDraw.DrawFieldEdges(cellList, Color.white);
            }
        }
    }

    public class CompProperties_AbilityMICLIC : CompProperties_AbilityEffect
    {
        public ThingDef projectileDef;

        public CompProperties_AbilityMICLIC()
        {
            compClass = typeof(CompAbilityEffect_MICLIC);
        }
    }
}
//咕咕又嘎嘎
