using HarmonyLib;
using RimWorld;
using Verse;

namespace USAC
{
    // 拦截轨道商船的物品投送
    // 建筑物使用运输夹投送
    [HarmonyPatch(typeof(TradeShip), nameof(TradeShip.GiveSoldThingToPlayer))]
    public static class Patch_TradeShipDelivery
    {
        public static bool Prefix(TradeShip __instance, Thing toGive, int countToGive, Pawn playerNegotiator)
        {
            if (__instance.TraderKind?.defName != "USAC_Trader_Orbital")
            {
                return true;
            }

            Map map = __instance.Map;
            if (map == null)
            {
                return true;
            }

            // 检查是否为建筑物或可拆卸建筑
            bool isBuilding = (toGive is MinifiedThing) || (toGive.def.category == ThingCategory.Building);

            if (isBuilding)
            {
                // 建筑物逐个处理
                int totalCount = countToGive;
                for (int i = 0; i < totalCount; i++)
                {
                    Thing thing = toGive.SplitOff(1);
                    thing.PreTraded(TradeAction.PlayerBuys, playerNegotiator, __instance);

                    Building building = ExtractBuilding(thing);
                    if (building != null)
                    {
                        USACDeliveryManager.Instance?.AddDelivery(building, map);
                    }
                    else
                    {
                        // 兜底处理
                        TradeUtility.SpawnDropPod(DropCellFinder.TradeDropSpot(map), map, thing);
                    }
                }
            }
            else
            {
                // 非建筑批量空投
                Thing thing = toGive.SplitOff(countToGive);
                thing.PreTraded(TradeAction.PlayerBuys, playerNegotiator, __instance);
                TradeUtility.SpawnDropPod(DropCellFinder.TradeDropSpot(map), map, thing);
            }

            return false;
        }

        private static Building ExtractBuilding(Thing thing)
        {
            // 拆卸形式的建筑物
            if (thing is MinifiedThing minified)
            {
                Log.Message($"[USAC] 是MinifiedThing，InnerThing: {minified.InnerThing?.def?.defName}");

                // 提取打包内建筑
                if (minified.InnerThing is Building building)
                {
                    // 从容器中移除建筑物
                    minified.GetDirectlyHeldThings().Remove(building);
                    Log.Message($"[USAC] 成功从MinifiedThing中提取建筑物: {building.def.defName}");

                    // 销毁空打包容器
                    minified.Destroy(DestroyMode.Vanish);

                    return building;
                }
            }

            // 直接是建筑物
            if (thing is Building directBuilding)
            {
                Log.Message($"[USAC] 直接是建筑物: {directBuilding.def.defName}");
                return directBuilding;
            }

            return null;
        }
    }
}
