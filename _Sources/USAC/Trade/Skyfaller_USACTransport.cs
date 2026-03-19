using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

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
            // 绘制被夹着的货物
            if (innerContainer.Count > 0)
            {
                Thing cargo = innerContainer[0];
                Vector3 cargoPos = drawLoc;
                cargoPos.y = Altitudes.AltitudeFor(AltitudeLayer.Building);
                DrawCargo(cargo, cargoPos);
            }
            
            // 绘制夹具本体
            Vector3 gripperPos = drawLoc;
            gripperPos.z += GripperOffsetZ * (gripperScale / 1.5f);
            gripperPos.y = Altitudes.AltitudeFor(AltitudeLayer.Skyfaller);
            
            GetScaledGraphic()?.Draw(gripperPos, Rot4.North, this);
        }

        private void DrawCargo(Thing cargo, Vector3 drawLoc)
        {
            if (cargo == null)
                return;
            
            Vector3 finalCargoPos = drawLoc;
            // 计算渲染网格偏移
            float offsetX = (cargo.def.size.x % 2 == 0) ? 0.5f : 0f;
            float offsetZ = (cargo.def.size.z % 2 == 0) ? 0.5f : 0f;
            
            // 考虑货物旋转偏移
            if (cargoRotation == Rot4.East || cargoRotation == Rot4.West)
            {
                float temp = offsetX;
                offsetX = offsetZ;
                offsetZ = temp;
            }
            
            finalCargoPos.x += offsetX;
            finalCargoPos.z += offsetZ;
            
            // 绘制货物保留高度值
            if (cargo is Building building)
            {
                building.Graphic?.Draw(finalCargoPos, cargoRotation, building);
            }
            else if (cargo is MinifiedThing minified && minified.InnerThing != null)
            {
                minified.InnerThing.Graphic?.Draw(finalCargoPos, cargoRotation, minified.InnerThing);
            }
            else
            {
                cargo.Graphic?.Draw(finalCargoPos, cargoRotation, cargo);
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
        private void CalculateGripperScale()
        {
            if (innerContainer.Count == 0)
                return;
            
            Thing cargo = innerContainer[0];
            
            // 提取实际建筑物
            Building building = cargo as Building;
            if (cargo is MinifiedThing minified)
                building = minified.InnerThing as Building;
            
            if (building != null)
            {
                // 根据建筑物尺寸计算缩放
                gripperScale = Mathf.Max(
                    building.def.size.x, 
                    building.def.size.z) * 1.2f;
                cargoRotation = building.Rotation;
            }
            else
            {
                gripperScale = 1.2f;
            }
        }

        private void SpawnContents(IntVec3 pos, Map map)
        {
            if (innerContainer == null || innerContainer.Count == 0)
                return;
            
            // 生成所有内容物
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
                    // 恢复物品的旋转
                    thing.Rotation = cargoRotation;
                    
                    // 直接生成在目标位置
                    GenPlace.TryPlaceThing(thing, pos, map, ThingPlaceMode.Direct);
                    
                    // 强制设置最终旋转（部分物体在 TryPlaceThing 后会重置）
                    if (thing is Building b)
                        b.Rotation = cargoRotation;
                }
            }
        }
        #endregion
    }
}
