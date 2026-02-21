using RimWorld;
using Verse;

namespace USAC
{
    // 定义机兵残骸组件属性
    public class CompProperties_MechWreck : CompProperties
    {
        // 引用死亡生成残骸定义
        public ThingDef wreckDef;

        public CompProperties_MechWreck()
        {
            compClass = typeof(CompMechWreck);
        }
    }

    // 定义机兵残骸逻辑组件
    // 实现死亡生成残骸逻辑
    public class CompMechWreck : ThingComp
    {
        // 记录死亡瞬间朝向数据
        private Rot4 cachedRotation = Rot4.Invalid;

        public CompProperties_MechWreck Props => (CompProperties_MechWreck)props;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);

            // 缓存当前朝向据缓存
            if (parent is Pawn pawn)
            {
                cachedRotation = pawn.Rotation;
            }
        }

        public override void CompTick()
        {
            base.CompTick();

            // 持续更新朝向缓存存
            if (parent is Pawn pawn && pawn.Spawned)
            {
                cachedRotation = pawn.Rotation;
            }
        }

        public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
        {
            base.Notify_Killed(prevMap, dinfo);

            if (Props.wreckDef == null || prevMap == null)
                return;

            Pawn pawn = parent as Pawn;
            if (pawn == null)
                return;

            // 获取获取机兵死亡点坐标
            IntVec3 deathPos = pawn.Position;

            // 使用缓存的朝向据
            Rot4 rotation = cachedRotation.IsValid ? cachedRotation : Rot4.South;

            // 搜寻合理的残骸放置坐标
            IntVec3 spawnPos;
            if (!TryFindSpawnPosition(prevMap, deathPos, Props.wreckDef, out spawnPos))
            {
                Log.Warning($"[USAC] 无法为 {pawn.LabelShort} 找到合适的残骸放置位置");
                return;
            }

            // 执行残骸建筑生成逻辑
            Thing wreck = ThingMaker.MakeThing(Props.wreckDef);

            // 传递着色数据
            var parentPaint = pawn.TryGetComp<Fortified.CompPaintable>();
            var wreckPaint = wreck.TryGetComp<Fortified.CompPaintable>();
            if (parentPaint != null && wreckPaint != null)
            {
                wreckPaint.color1 = parentPaint.color1;
                wreckPaint.color2 = parentPaint.color2;
                wreckPaint.color3 = parentPaint.color3;
                wreckPaint.camoDef = parentPaint.camoDef;
                wreckPaint.brightness = parentPaint.brightness;
            }

            // 设置派系为机兵的派系
            if (pawn.Faction != null)
            {
                wreck.SetFaction(pawn.Faction);
            }

            GenSpawn.Spawn(wreck, spawnPos, prevMap, rotation);

            // 执行机兵原始尸体销毁
            if (pawn.Corpse != null && !pawn.Corpse.Destroyed)
            {
                pawn.Corpse.Destroy();
            }
        }

        // 搜寻有效残骸放置点
        private bool TryFindSpawnPosition(Map map, IntVec3 center, ThingDef thingDef, out IntVec3 result)
        {
            // 校验原始死亡位置有效性
            if (CanPlaceAt(map, center, thingDef))
            {
                result = center;
                return true;
            }

            // 扩展范围搜寻放置坐标
            // 递增半径搜寻可用区域
            int maxRadius = System.Math.Max(map.Size.x, map.Size.z);

            return CellFinder.TryFindRandomCellNear(
                center,
                map,
                maxRadius,
                (IntVec3 c) => CanPlaceAt(map, c, thingDef),
                out result
            );
        }

        // 校验指定位置放置有效性
        private bool CanPlaceAt(Map map, IntVec3 pos, ThingDef thingDef)
        {
            if (!pos.InBounds(map))
                return false;

            // 校验地形是否允许通达
            if (!pos.Standable(map))
                return false;

            // 校验是否存在阻挡建筑物
            Building building = pos.GetEdifice(map);
            if (building != null)
                return false;

            // 校验是否存在冲突物品
            var thingsAt = pos.GetThingList(map);
            foreach (Thing thing in thingsAt)
            {
                // 忽略生物与尸体冲突
                if (thing is Pawn || thing is Corpse)
                    continue;

                // 判定物品冲突禁止放置
                if (thing.def.category == ThingCategory.Item)
                    return false;

                // 判定建筑冲突禁止放置
                if (thing.def.category == ThingCategory.Building)
                    return false;
            }

            return true;
        }
    }
}
