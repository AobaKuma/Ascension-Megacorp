using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace USAC
{
    public class CompProperties_VisualPawnContainer : CompProperties
    {
        // 记录建筑前排挂载槽格位置
        public List<Vector2> frontRowOffsets = new List<Vector2>();

        // 记录前排显示对象最大缩放
        public float frontRowMaxSize = 1.2f;

        // 记录高优先级渲染槽格坐标
        public List<Vector2> higherOffsets = new List<Vector2>();

        // 记录低优先级渲染槽格坐标
        public List<Vector2> lowerOffsets = new List<Vector2>();

        // 记录建筑顶部可用槽位总数
        public int topSlotCount = 1;

        // 记录顶部物体堆叠高度偏移
        public float stackZOffset = 4f;

        // 引用容器覆盖贴图资产路径
        public string overlayTexPath;

        // 记录覆盖贴图渲染尺寸数值
        public Vector2 overlayDrawSize = new Vector2(9, 9);

        // 记录覆盖贴图高度坐标偏移
        public float overlayZOffset = 1f;

        // 绑定组件逻辑实现类定义
        public CompProperties_VisualPawnContainer()
        {
            compClass = typeof(CompVisualPawnContainer);
        }
    }

    // 注册至地图渲染组件
    public class CompVisualPawnContainer : ThingComp
    {
        public CompProperties_VisualPawnContainer Props => (CompProperties_VisualPawnContainer)props;

        // 缓冲容器覆盖图形资产数据
        private Graphic overlayGraphic;
        public Graphic OverlayGraphic
        {
            get
            {
                if (overlayGraphic == null && !string.IsNullOrEmpty(Props.overlayTexPath))
                {
                    Shader shader = ShaderDatabase.Transparent;
                    overlayGraphic = GraphicDatabase.Get<Graphic_Single>(
                        Props.overlayTexPath,
                        shader,
                        Props.overlayDrawSize,
                        Color.white);
                }
                return overlayGraphic;
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            // 注册至全局可视化容器管理
            parent.Map.GetComponent<MapComponent_VisualPawnMounts>().Register(this);
        }

        public override void PostDestroy(DestroyMode mode, Map map)
        {
            base.PostDestroy(mode, map);
            // 移除全局可视化容器管理项
            if (map != null)
            {
                map.GetComponent<MapComponent_VisualPawnMounts>().Unregister(this);
            }
        }
    }
}
