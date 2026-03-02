using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld.Planet;

namespace USAC
{
    // 执行容器内机兵挂载视觉渲染
    public class MapComponent_VisualPawnMounts : MapComponent
    {
        private HashSet<CompVisualPawnContainer> registeredComps = new HashSet<CompVisualPawnContainer>();

        // 声明并复用渲染缓存数据列表
        private List<PawnData> cachedPawnData = new List<PawnData>();
        private List<Pawn> cachedPawnList = new List<Pawn>();

        private struct PawnData
        {
            public Pawn Pawn;
            public float Volume;
            public float EffectiveSize;
        }

        public MapComponent_VisualPawnMounts(Map map) : base(map)
        {
        }

        public void Register(CompVisualPawnContainer comp) => registeredComps.Add(comp);
        public void Unregister(CompVisualPawnContainer comp) => registeredComps.Remove(comp);

        public override void MapComponentUpdate()
        {
            if (registeredComps.Count == 0) return;
            if (Find.CurrentMap != map || WorldRendererUtility.WorldRendered) return;

            foreach (var comp in registeredComps)
            {
                if (comp.parent is IThingHolder holder)
                {
                    var container = holder.GetDirectlyHeldThings();
                    if (container != null && container.Count > 0)
                    {
                        cachedPawnList.Clear();
                        foreach (var thing in container)
                        {
                            if (thing is Pawn p) cachedPawnList.Add(p);
                        }
                        DrawMountedPawns(comp, cachedPawnList, comp.parent.DrawPos);
                    }
                }
                DrawOverlay(comp, comp.parent.DrawPos);
            }
        }

        // 执行建筑顶层覆贴图绘制逻辑
        private void DrawOverlay(CompVisualPawnContainer comp, Vector3 centerPos)
        {
            var overlay = comp.OverlayGraphic;
            if (overlay == null) return;

            Vector3 pos = centerPos;
            pos.z += comp.Props.overlayZOffset;
            pos.y += 1f;

            overlay.Draw(pos, Rot4.North, comp.parent);
        }

        private void DrawMountedPawns(CompVisualPawnContainer comp, List<Pawn> pawns, Vector3 centerPos)
        {
            if (pawns.Count == 0) return;

            var props = comp.Props;

            // 检索并分析机兵静态渲染数据
            cachedPawnData.Clear();
            for (int i = 0; i < pawns.Count; i++)
            {
                var p = pawns[i];
                Vector2 drawSizeVec = p.Drawer.renderer.BodyGraphic?.drawSize ?? Vector2.one;
                float ds = drawSizeVec.x;
                cachedPawnData.Add(new PawnData
                {
                    Pawn = p,
                    Volume = ds * drawSizeVec.y,
                    EffectiveSize = ds - 1f
                });
            }

            // 执行挂载列表按体型权重排序
            cachedPawnData.Sort((a, b) => b.Volume.CompareTo(a.Volume));

            List<Pawn> overflowPawns = new List<Pawn>();
            List<PawnData> smallPawns = new List<PawnData>();

            // 执行挂载列表对象的功能分类
            int tinyIndex = 0;
            for (int i = 0; i < cachedPawnData.Count; i++)
            {
                var data = cachedPawnData[i];
                if (data.EffectiveSize <= props.frontRowMaxSize)
                {
                    // 执行容器前排槽位渲染与填充
                    if (props.frontRowOffsets != null && tinyIndex < props.frontRowOffsets.Count)
                    {
                        Vector2 offset = props.frontRowOffsets[tinyIndex];
                        RenderPawn(data.Pawn, centerPos + new Vector3(offset.x, 0.5f, offset.y + 1.3f));
                        tinyIndex++;
                    }
                    else
                    {
                        smallPawns.Add(data);
                    }
                }
                else if (data.EffectiveSize <= 1.5f)
                {
                    smallPawns.Add(data);
                }
                else
                {
                    // 执行特大型挂载单位状态标记
                }
            }

            // 执行高低层混合挂载槽格渲染
            int smallIndex = 0;
            if (props.higherOffsets != null)
            {
                for (int i = 0; i < props.higherOffsets.Count && smallIndex < smallPawns.Count; i++, smallIndex++)
                {
                    Vector2 offset = props.higherOffsets[i];
                    RenderPawn(smallPawns[smallIndex].Pawn, centerPos + new Vector3(offset.x, 0.5f, offset.y + 1.3f));
                }
            }

            if (props.lowerOffsets != null)
            {
                for (int i = 0; i < props.lowerOffsets.Count && smallIndex < smallPawns.Count; i++, smallIndex++)
                {
                    Vector2 offset = props.lowerOffsets[i];
                    RenderPawn(smallPawns[smallIndex].Pawn, centerPos + new Vector3(offset.x, -0.5f, offset.y + 1.3f));
                }
            }

            // 检索无法入槽的额外挂载单位
            while (smallIndex < smallPawns.Count)
            {
                overflowPawns.Add(smallPawns[smallIndex].Pawn);
                smallIndex++;
            }

            // 执行顶部大型挂载槽渲染逻辑
            var topPawns = cachedPawnData.Where(d => d.EffectiveSize > 1.5f).Select(d => d.Pawn)
                                        .Concat(overflowPawns)
                                        .OrderByDescending(p => (p.Drawer.renderer.BodyGraphic?.drawSize.x ?? 1f) * (p.Drawer.renderer.BodyGraphic?.drawSize.y ?? 1f))
                                        .Take(props.topSlotCount)
                                        .ToList();

            float stackOffset = 0f;
            float layerOffset = 0f;

            for (int i = 0; i < topPawns.Count; i++)
            {
                Pawn p = topPawns[i];
                float ds = p.Drawer.renderer.BodyGraphic?.drawSize.x ?? 1f;

                if (i > 0) stackOffset += ds * 0.5f;

                Vector3 pos = centerPos;
                pos.z += props.stackZOffset + stackOffset;
                pos.y += 0.5f + layerOffset;

                RenderPawn(p, pos);
                layerOffset += 0.1f;
            }
        }

        private void RenderPawn(Pawn pawn, Vector3 pos)
        {
            pawn.Drawer.renderer.RenderPawnAt(pos, Rot4.South, true);
        }
    }
}
