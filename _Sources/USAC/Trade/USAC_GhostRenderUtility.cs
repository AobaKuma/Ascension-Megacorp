using Fortified;
using RimWorld;
using UnityEngine;
using Verse;

namespace USAC
{
    // 虚影渲染工具 规避NRE
    internal static class USAC_GhostRenderUtility
    {
        #region 颜色常量
        internal static readonly Color BlueprintBlue = new Color(0.35f, 0.75f, 1f, 0.35f);
        internal static readonly Color CanPlaceGreen = new Color(0.5f, 1f, 0.6f, 0.4f);
        internal static readonly Color CannotPlaceRed = new Color(1f, 0f, 0f, 0.4f);
        internal static readonly Color ConfirmedGray = new Color(0.5f, 0.5f, 0.5f, 0.4f);
        #endregion

        #region 公共接口
        // 渲染虚影 自动识别货物
        internal static void DrawGhost(IntVec3 pos, Rot4 rot, Thing cargo, Color color)
        {
            if (cargo == null) return;

            // 劫持容器图形 渲染机兵虚影
            if (cargo is Building_MechCapsule capsule && capsule.Mech != null)
            {
                Graphic mechGraphic = capsule.Mech.kindDef.lifeStages[0].bodyGraphicData?.Graphic 
                                     ?? capsule.Mech.def.graphic;
                
                if (mechGraphic != null)
                {
                    GhostDrawer.DrawGhostThing(pos, rot, cargo.def, mechGraphic, color, AltitudeLayer.Blueprint);
                    return;
                }
            }

            // 打包物品 渲染内部建筑
            ThingDef visualDef = cargo.def;
            if (cargo is MinifiedThing minified && minified.InnerThing != null)
                visualDef = minified.InnerThing.def;

            // 检查图形资源
            if (visualDef?.graphic == null) return;

            // 调用标准虚影绘制
            GhostDrawer.DrawGhostThing(pos, rot, visualDef, null, color, AltitudeLayer.Blueprint);
        }
        #endregion
    }
}
