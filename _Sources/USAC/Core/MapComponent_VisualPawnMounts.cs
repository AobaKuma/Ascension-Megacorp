using System.Collections.Generic;
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
        private List<Pawn> overflowPawns = new List<Pawn>();
        private List<PawnData> smallPawns = new List<PawnData>();
        private List<Pawn> topPawns = new List<Pawn>();

        // 缓存排序结果
        private Dictionary<CompVisualPawnContainer, CachedLayout> layoutCache = new Dictionary<CompVisualPawnContainer, CachedLayout>();

        private struct PawnData
        {
            public Pawn Pawn;
            public float Volume;
            public float EffectiveSize;
        }

        private class CachedLayout
        {
            public List<(Pawn pawn, Vector3 offset)> renderList = new List<(Pawn, Vector3)>();
        }

        public MapComponent_VisualPawnMounts(Map map) : base(map)
        {
        }

        public void Register(CompVisualPawnContainer comp) => registeredComps.Add(comp);
        
        public void Unregister(CompVisualPawnContainer comp)
        {
            registeredComps.Remove(comp);
            layoutCache.Remove(comp);
        }

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

                        // 检查是否需要重新计算布局
                        bool needsRecalc = comp.CheckContainerChanged() || !layoutCache.ContainsKey(comp);
                        
                        if (needsRecalc)
                        {
                            CalculateLayout(comp, cachedPawnList);
                        }

                        DrawCachedLayout(comp, comp.parent.DrawPos);
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

        // 计算布局并缓存
        private void CalculateLayout(CompVisualPawnContainer comp, List<Pawn> pawns)
        {
            if (!layoutCache.TryGetValue(comp, out var cache))
            {
                cache = new CachedLayout();
                layoutCache[comp] = cache;
            }

            cache.renderList.Clear();
            if (pawns.Count == 0) return;

            var props = comp.Props;

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

            cachedPawnData.Sort((a, b) => b.Volume.CompareTo(a.Volume));

            overflowPawns.Clear();
            smallPawns.Clear();

            int tinyIndex = 0;
            for (int i = 0; i < cachedPawnData.Count; i++)
            {
                var data = cachedPawnData[i];
                if (data.EffectiveSize <= props.frontRowMaxSize)
                {
                    if (props.frontRowOffsets != null && tinyIndex < props.frontRowOffsets.Count)
                    {
                        Vector2 offset = props.frontRowOffsets[tinyIndex];
                        cache.renderList.Add((data.Pawn, new Vector3(offset.x, 0.5f, offset.y + 1.3f)));
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
            }

            int smallIndex = 0;
            if (props.higherOffsets != null)
            {
                for (int i = 0; i < props.higherOffsets.Count && smallIndex < smallPawns.Count; i++, smallIndex++)
                {
                    Vector2 offset = props.higherOffsets[i];
                    cache.renderList.Add((smallPawns[smallIndex].Pawn, new Vector3(offset.x, 0.5f, offset.y + 1.3f)));
                }
            }

            if (props.lowerOffsets != null)
            {
                for (int i = 0; i < props.lowerOffsets.Count && smallIndex < smallPawns.Count; i++, smallIndex++)
                {
                    Vector2 offset = props.lowerOffsets[i];
                    cache.renderList.Add((smallPawns[smallIndex].Pawn, new Vector3(offset.x, -0.5f, offset.y + 1.3f)));
                }
            }

            while (smallIndex < smallPawns.Count)
            {
                overflowPawns.Add(smallPawns[smallIndex].Pawn);
                smallIndex++;
            }

            topPawns.Clear();
            for (int i = 0; i < cachedPawnData.Count; i++)
            {
                if (cachedPawnData[i].EffectiveSize > 1.5f)
                    topPawns.Add(cachedPawnData[i].Pawn);
            }
            for (int i = 0; i < overflowPawns.Count; i++)
                topPawns.Add(overflowPawns[i]);

            topPawns.Sort((a, b) =>
            {
                float va = (a.Drawer.renderer.BodyGraphic?.drawSize.x ?? 1f) * (a.Drawer.renderer.BodyGraphic?.drawSize.y ?? 1f);
                float vb = (b.Drawer.renderer.BodyGraphic?.drawSize.x ?? 1f) * (b.Drawer.renderer.BodyGraphic?.drawSize.y ?? 1f);
                return vb.CompareTo(va);
            });
            
            int topCount = topPawns.Count > props.topSlotCount ? props.topSlotCount : topPawns.Count;
            float stackOffset = 0f;
            float layerOffset = 0f;

            for (int i = 0; i < topCount; i++)
            {
                Pawn p = topPawns[i];
                float ds = p.Drawer.renderer.BodyGraphic?.drawSize.x ?? 1f;
                if (i > 0) stackOffset += ds * 0.5f;

                Vector3 offset = new Vector3(0, 0.5f + layerOffset, props.stackZOffset + stackOffset);
                cache.renderList.Add((p, offset));
                layerOffset += 0.1f;
            }
        }

        // 使用缓存布局渲染
        private void DrawCachedLayout(CompVisualPawnContainer comp, Vector3 centerPos)
        {
            if (!layoutCache.TryGetValue(comp, out var cache)) return;

            for (int i = 0; i < cache.renderList.Count; i++)
            {
                var (pawn, offset) = cache.renderList[i];
                RenderPawn(pawn, centerPos + offset);
            }
        }

        private void RenderPawn(Pawn pawn, Vector3 pos)
        {
            pawn.Drawer.renderer.RenderPawnAt(pos, Rot4.South, true);
        }
    }
}
