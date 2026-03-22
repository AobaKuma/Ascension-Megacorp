using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using Fortified;

namespace USAC
{
    // USAC轨道运输夹
    // 用于运输购买的建筑物和机兵订单
    public class Skyfaller_USACTransport : Skyfaller
    {
        #region 字段
        private float gripperScale = 1.5f;
        private Rot4 cargoRotation = Rot4.North;
        private Graphic cachedScaledGraphic;
        private float cachedScaleKey = -1f;
        private const float GripperOffsetZ = 0.5f;
        #endregion

        #region 生命周期
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            
            if (!respawningAfterLoad)
            {
                // 设置为友方避免被防空炮击落
                factionInt = Faction.OfPlayer;
                
                // 计算夹具缩放
                CalculateGripperScale();
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref gripperScale, "gripperScale", 1.5f);
            Scribe_Values.Look(ref cargoRotation, "cargoRotation", Rot4.North);
        }

        protected override void Impact()
        {
            Map map = Map;
            IntVec3 pos = Position;
            
            // 破拆屋顶
            if (pos.Roofed(map))
            {
                var roof = pos.GetRoof(map);
                map.roofGrid.SetRoof(pos, null);
                if (roof != null && roof.isThickRoof)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        FleckMaker.ThrowDustPuff(
                            pos.ToVector3Shifted() + Gen.RandomHorizontalVector(0.5f),
                            map, 1.5f);
                    }
                }
            }
            
            // 播放着陆音效
            FleckMaker.ThrowDustPuff(pos.ToVector3Shifted(), map, 2.5f);
            
            if (def.skyfaller.impactSound != null)
            {
                def.skyfaller.impactSound.PlayOneShot(
                    SoundInfo.InMap(new TargetInfo(pos, map)));
            }
            
            // 生成内容物
            SpawnContents(pos, map);
            
            base.Impact();
        }
        #endregion

        #region 渲染
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            float offsetX = 0f;
            float offsetZ = 0f;

            // 计算偶数建筑对齐偏移
            if (innerContainer.Count > 0)
            {
                Thing cargo = innerContainer[0];
                offsetX = (cargo.def.size.x % 2 == 0) ? 0.5f : 0f;
                offsetZ = (cargo.def.size.z % 2 == 0) ? 0.5f : 0f;

                if (cargoRotation.IsHorizontal)
                {
                    float temp = offsetX;
                    offsetX = offsetZ;
                    offsetZ = temp;
                }
            }

            // 绘制地面落点虚像
            DrawGroundBlueprint();

            // 绘制被夹着的货物
            if (innerContainer.Count > 0)
            {
                Thing cargo = innerContainer[0];
                Vector3 cargoPos = drawLoc;
                cargoPos.x += offsetX;
                cargoPos.z += offsetZ;
                cargoPos.y = Altitudes.AltitudeFor(AltitudeLayer.Building);
                DrawCargo(cargo, cargoPos);
            }
            
            // 绘制夹具本体
            Vector3 gripperPos = drawLoc;
            gripperPos.x += offsetX;
            gripperPos.z += offsetZ;
            gripperPos.z += GripperOffsetZ * (gripperScale / 1.5f);
            gripperPos.y = Altitudes.AltitudeFor(AltitudeLayer.Skyfaller);
            
            GetScaledGraphic()?.Draw(gripperPos, Rot4.North, this);
        }

        private void DrawGroundBlueprint()
        {
            if (innerContainer == null || innerContainer.Count == 0) return;
            
            Thing cargo = innerContainer[0];
            
            // 调用安全虚影渲染
            USAC_GhostRenderUtility.DrawGhost(
                Position, 
                cargoRotation, 
                cargo, 
                USAC_GhostRenderUtility.BlueprintBlue);
        }

        private void DrawCargo(Thing cargo, Vector3 drawLoc)
        {
            if (cargo == null)
                return;
            
            // 递归渲染嵌套容器
            if (cargo is Building_MechCapsule capsule)
            {
                capsule.DynamicDrawPhaseAt(DrawPhase.Draw, drawLoc, false);
                return;
            }

            if (cargo is Building building)
            {
                building.Graphic?.Draw(drawLoc, cargoRotation, building);
            }
            else if (cargo is MinifiedThing minified && minified.InnerThing != null)
            {
                minified.InnerThing.Graphic?.Draw(drawLoc, cargoRotation, minified.InnerThing);
            }
            else
            {
                cargo.Graphic?.Draw(drawLoc, cargoRotation, cargo);
            }
        }

        private Graphic GetScaledGraphic()
        {
            if (Graphic == null) return null;
            
            if (cachedScaledGraphic == null || cachedScaleKey != gripperScale)
            {
                cachedScaledGraphic = Graphic.GetCopy(
                    new Vector2(gripperScale, gripperScale), null);
                cachedScaleKey = gripperScale;
            }
            
            return cachedScaledGraphic;
        }
        #endregion

        #region 辅助方法
        public static float CalculateGripperScaleFor(Thing cargo)
        {
            if (cargo == null) return 1.5f;

            IntVec2 size = cargo.def.size;
            if (cargo is MinifiedThing minified)
                size = minified.InnerThing.def.size;
            
            // 基于建筑最窄轴进行缩放
            float minAxis = Mathf.Min(size.x, size.z);
            return minAxis * 1.5f + 0.5f;
        }

        private void CalculateGripperScale()
        {
            if (innerContainer.Count == 0)
                return;
            
            Thing cargo = innerContainer[0];
            gripperScale = CalculateGripperScaleFor(cargo);

            if (cargo is Building building)
                cargoRotation = building.Rotation;
            else if (cargo is MinifiedThing minified)
                cargoRotation = minified.InnerThing.Rotation;
        }

        private void SpawnContents(IntVec3 pos, Map map)
        {
            if (innerContainer == null || innerContainer.Count == 0)
                return;
            
            List<Thing> toSpawn = new List<Thing>();
            foreach (Thing thing in innerContainer)
            {
                toSpawn.Add(thing);
            }
            
            innerContainer.Clear();
            
            foreach (Thing thing in toSpawn)
            {
                if (thing != null)
                {
                    thing.Rotation = cargoRotation;
                    GenPlace.TryPlaceThing(thing, pos, map, ThingPlaceMode.Direct);
                    if (thing is Building b)
                        b.Rotation = cargoRotation;
                }
            }
        }
        #endregion
    }
}
