using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using Fortified;

namespace USAC
{
    // USAC建筑部署指示器
    public class Designator_PlaceUSACDelivery : Designator
    {
        #region 字段
        private Thing thingToPlace;
        private Rot4 placementRot;
        private static readonly Color CanPlaceColor = new Color(0.5f, 1f, 0.6f, 0.4f);
        private static readonly Color CannotPlaceColor = new Color(1f, 0f, 0f, 0.4f);
        #endregion

        #region 属性
        public override bool DragDrawMeasurements => false;
        
        private ThingDef PlacingDef
        {
            get
            {
                if (thingToPlace is Building building)
                    return building.def;
                return thingToPlace?.def;
            }
        }
        #endregion

        #region 构造
        public Designator_PlaceUSACDelivery(Thing thing)
        {
            thingToPlace = thing;
            placementRot = Rot4.North;
            
            if (thing != null)
            {
                defaultLabel = thing.LabelCap;
                defaultDesc = "USAC.Trade.PlaceDelivery".Translate(thing.LabelCap);
                icon = thing.def.uiIcon;
                iconAngle = thing.def.uiIconAngle;
                iconDrawScale = GenUI.IconDrawScale(thing.def);
            }
        }
        #endregion

        #region 放置逻辑
        public override AcceptanceReport CanDesignateCell(IntVec3 loc)
        {
            if (!loc.InBounds(Find.CurrentMap))
                return false;
            
            if (thingToPlace == null || PlacingDef == null)
                return false;
            
            // 检查基本放置条件
            AcceptanceReport baseReport = GenConstruct.CanPlaceBlueprintAt(
                PlacingDef, loc, placementRot, Find.CurrentMap);
            
            if (!baseReport.Accepted)
                return baseReport;
            
            // 检查待确认空投重叠
            if (USACDeliveryManager.Instance != null)
            {
                CellRect myRect = GenAdj.OccupiedRect(loc, placementRot, PlacingDef.size);
                foreach (var delivery in USACDeliveryManager.Instance.PendingDeliveries)
                {
                    if (delivery.confirmed && delivery.thing != null)
                    {
                        CellRect otherRect = GenAdj.OccupiedRect(delivery.targetPos, delivery.targetRot, delivery.thing.def.size);
                        if (myRect.Overlaps(otherRect))
                        {
                            return "USAC.Trade.CannotOverlapWithPending".Translate();
                        }
                    }
                }
            }

            // 检查厚岩顶
            CellRect occupiedRect = GenAdj.OccupiedRect(loc, placementRot, PlacingDef.size);
            foreach (IntVec3 cell in occupiedRect)
            {
                RoofDef roof = cell.GetRoof(Find.CurrentMap);
                if (roof != null && roof.isThickRoof)
                {
                    return "USAC.Trade.CannotPlaceUnderThickRoof".Translate();
                }
            }

            // 检查现有建筑物
            Map map = Find.CurrentMap;
            foreach (IntVec3 cell in occupiedRect)
            {
                foreach (Thing existing in cell.GetThingList(map))
                {
                    // 允许压盖植物
                    if (existing.def.category == ThingCategory.Plant)
                        continue;

                    // 存在建筑物则拒绝
                    if (existing.def.category == ThingCategory.Building)
                    {
                        return "USAC.Trade.CannotLandOnBuilding".Translate();
                    }
                }
            }
            
            return true;
        }

        public override void DesignateSingleCell(IntVec3 loc)
        {
            USACDeliveryManager.Instance?.ConfirmPlacement(thingToPlace, loc, placementRot);
        }

        public override void SelectedUpdate()
        {
            GenDraw.DrawNoBuildEdgeLines();
            
            // 绘制已确认交付虚影
            DrawConfirmedDeliveries();

            IntVec3 cell = UI.MouseCell();
            if (!cell.InBounds(Find.CurrentMap))
                return;
            
            // 绘制选点虚像
            Color ghostCol = CanDesignateCell(cell).Accepted ? CanPlaceColor : CannotPlaceColor;
            DrawGhost(ghostCol);
            
            // 绘制交互格子提示
            if (PlacingDef != null)
            {
                GenDraw.DrawInteractionCells(PlacingDef, cell, placementRot);
            }
        }

        private void DrawConfirmedDeliveries()
        {
            var manager = USACDeliveryManager.Instance;
            if (manager == null) return;

            // 统一使用公共的安全虚影渲染通道
            foreach (var delivery in manager.PendingDeliveries)
            {
                if (delivery.confirmed && delivery.thing != null)
                {
                    USAC_GhostRenderUtility.DrawGhost(
                        delivery.targetPos, 
                        delivery.targetRot, 
                        delivery.thing, 
                        USAC_GhostRenderUtility.ConfirmedGray);
                }
            }
        }

        public override void Selected()
        {
            base.Selected();
            // 清除选择防止冲突
            Find.Selector.ClearSelection();
        }

        public override void SelectedProcessInput(Event ev)
        {
            // 拦截键盘输入响应旋转一线说明
            if (KeyBindingDefOf.Designator_RotateLeft.KeyDownEvent)
            {
                HandleRotation(RotationDirection.Counterclockwise);
                ev.Use();
                return;
            }
            if (KeyBindingDefOf.Designator_RotateRight.KeyDownEvent)
            {
                HandleRotation(RotationDirection.Clockwise);
                ev.Use();
                return;
            }

            // 中键旋转支持
            if (ev.type == EventType.MouseDown && ev.button == 2)
            {
                HandleRotation(RotationDirection.Clockwise);
                ev.Use();
                return;
            }
        }

        private void HandleRotation(RotationDirection dir)
        {
            SoundDefOf.DragSlider.PlayOneShotOnCamera();
            placementRot.Rotate(dir);
            
            // 同步物品旋转
            if (thingToPlace != null)
            {
                // 同步打包物品旋转
                if (thingToPlace is MinifiedThing minified && minified.InnerThing != null)
                {
                    minified.InnerThing.Rotation = placementRot;
                }
                thingToPlace.Rotation = placementRot;
            }
        }

        #endregion

        #region DrawGhostSystem
        protected virtual void DrawGhost(Color ghostCol)
        {
            if (thingToPlace == null) return;
            // 接入公共渲染引擎
            USAC_GhostRenderUtility.DrawGhost(UI.MouseCell(), placementRot, thingToPlace, ghostCol);
        }

        public override void ProcessInput(Event ev)
        {
            base.ProcessInput(ev);
        }

        public override void DrawMouseAttachments()
        {
            base.DrawMouseAttachments();
        }
        #endregion
    }
}
