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
            Log.Message($"[USAC] Patch_TradeShipDelivery 被调用: {toGive?.def?.defName}, TraderKind: {__instance.TraderKind?.defName}");
            
            // 检查是否为USAC商船
            if (__instance.TraderKind?.defName != "USAC_Trader_Orbital")
            {
                Log.Message($"[USAC] 不是USAC商船，使用原版逻辑");
                return true;
            }
            
            Map map = __instance.Map;
            if (map == null)
            {
                Log.Warning($"[USAC] Map为空");
                return true;
            }
            
            // 分离物品
            Thing thing = toGive.SplitOff(countToGive);
            thing.PreTraded(TradeAction.PlayerBuys, playerNegotiator, __instance);
            
            Log.Message($"[USAC] 物品类型: {thing.GetType().Name}, def: {thing.def.defName}");
            
            // 提取建筑物
            Building building = ExtractBuilding(thing);
            
            if (building != null)
            {
                Log.Message($"[USAC] 检测到建筑物: {building.def.defName}，添加到交付队列");
            }
            else
            {
                Log.Message($"[USAC] 不是建筑物，使用空投舱");
            }
            
            // 如果是建筑物使用运输夹
            if (building != null)
            {
                USACDeliveryManager.Instance?.AddDelivery(building, map);
                return false;
            }
            
            // 其他物品使用原版空投舱
            TradeUtility.SpawnDropPod(DropCellFinder.TradeDropSpot(map), map, thing);
            return false;
        }
        
        private static Building ExtractBuilding(Thing thing)
        {
            // 拆卸形式的建筑物
            if (thing is MinifiedThing minified)
            {
                Log.Message($"[USAC] 是MinifiedThing，InnerThing: {minified.InnerThing?.def?.defName}");
                
                // 从MinifiedThing中提取建筑物
                if (minified.InnerThing is Building building)
                {
                    // 从容器中移除建筑物
                    minified.GetDirectlyHeldThings().Remove(building);
                    Log.Message($"[USAC] 成功从MinifiedThing中提取建筑物: {building.def.defName}");
                    
                    // 销毁空的MinifiedThing
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
