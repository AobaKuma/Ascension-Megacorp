using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

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
            
            // 检查厚岩顶
            CellRect occupiedRect = GenAdj.OccupiedRect(loc, placementRot, PlacingDef.Size);
            foreach (IntVec3 cell in occupiedRect)
            {
                RoofDef roof = cell.GetRoof(Find.CurrentMap);
                if (roof != null && roof.isThickRoof)
                {
                    return "USAC.Trade.CannotPlaceUnderThickRoof".Translate();
                }
            }
            
            return true;
        }

        public override void DesignateSingleCell(IntVec3 loc)
        {
            USACDeliveryManager.Instance?.ConfirmPlacement(thingToPlace, loc, placementRot);
            Find.DesignatorManager.Deselect();
        }

        public override void SelectedUpdate()
        {
            GenDraw.DrawNoBuildEdgeLines();
            
            // 处理旋转快捷键
            HandleRotationShortcuts();
            
            IntVec3 cell = UI.MouseCell();
            if (!cell.InBounds(Find.CurrentMap))
                return;
            
            // 绘制幽灵预览
            Color ghostCol = CanDesignateCell(cell).Accepted ? CanPlaceColor : CannotPlaceColor;
            DrawGhost(ghostCol);
            
            // 绘制交互格子
            if (PlacingDef != null)
            {
                GenDraw.DrawInteractionCells(PlacingDef, cell, placementRot);
            }
        }

        private void HandleRotationShortcuts()
        {
            RotationDirection rotationDirection = RotationDirection.None;
            
            // Q键逆时针旋转
            if (KeyBindingDefOf.Designator_RotateLeft.KeyDownEvent)
            {
                rotationDirection = RotationDirection.Counterclockwise;
                Event.current.Use();
            }
            
            // E键顺时针旋转
            if (KeyBindingDefOf.Designator_RotateRight.KeyDownEvent)
            {
                rotationDirection = RotationDirection.Clockwise;
                Event.current.Use();
            }
            
            // 中键点击旋转
            if (Event.current.type == EventType.MouseDown && Event.current.button == 2)
            {
                rotationDirection = RotationDirection.Clockwise;
                Event.current.Use();
            }
            
            if (rotationDirection != RotationDirection.None)
            {
                HandleRotation(rotationDirection);
                // 强制消耗事件防止双向触发
                Event.current.Use();
            }
        }

        private void HandleRotation(RotationDirection dir)
        {
            SoundDefOf.DragSlider.PlayOneShotOnCamera();
            placementRot.Rotate(dir);
            
            // 更新物品实体旋转
            if (thingToPlace != null)
            {
                thingToPlace.Rotation = placementRot;
            }
        }

        protected virtual void DrawGhost(Color ghostCol)
        {
            IntVec3 cell = UI.MouseCell();
            
            if (PlacingDef != null)
            {
                GhostDrawer.DrawGhostThing(
                    cell, 
                    placementRot, 
                    PlacingDef, 
                    null, 
                    ghostCol, 
                    AltitudeLayer.Blueprint);
            }
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
