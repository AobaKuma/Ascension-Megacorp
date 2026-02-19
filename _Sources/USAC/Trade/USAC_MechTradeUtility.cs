using Fortified;
using RimWorld;
using Verse;

namespace USAC
{
    // USAC 机兵交易工具类
    public static class USAC_MechTradeUtility
    {
        // 投送机兵
        public static void DropMech(PawnKindDef mechKindDef, Pawn negotiator)
        {
            Map map = negotiator?.Map;
            if (map == null)
            {
                Log.Error("[USAC] DropMech: negotiator has no map");
                return;
            }

            if (mechKindDef == null)
            {
                Log.Error("[USAC] DropMech: mechKindDef is null");
                return;
            }

            // 获取合适尺寸的容器 Def
            ThingDef capsuleDef = MechCapsuleUtility.GetCapsuleDefForKind(mechKindDef);
            if (capsuleDef == null)
            {
                Log.Error($"[USAC] DropMech: no suitable capsule def for {mechKindDef.defName}");
                return;
            }

            // 生成容器
            Building_MechCapsule capsule = (Building_MechCapsule)ThingMaker.MakeThing(capsuleDef);
            capsule.SetFaction(Faction.OfPlayer);

            // 生成机兵并放入容器
            Pawn mech = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                mechKindDef,
                Faction.OfPlayer,
                PawnGenerationContext.NonPlayer,
                map.Tile,
                forceGenerateNewPawn: true,
                fixedBiologicalAge: 0,
                fixedChronologicalAge: 0
            ));
            capsule.TryAcceptMech(mech);

            // 查找空投位置
            IntVec3 dropSpot = FindDropSpotForSize(map, capsuleDef.size, negotiator.Position);

            // 创建空投舱
            SkyfallerMaker.SpawnSkyfaller(USAC_DefOf.USAC_MechIncoming, capsule, dropSpot, map);

            Messages.Message(
                "USAC_MechDelivered".Translate(mechKindDef.label),
                new TargetInfo(dropSpot, map),
                MessageTypeDefOf.PositiveEvent
            );
        }

        // 查找合适空投点
        private static IntVec3 FindDropSpotForSize(Map map, IntVec2 size, IntVec3 nearLoc)
        {
            // 优先交易点附近
            IntVec3 targetSpot = nearLoc.IsValid ? nearLoc : DropCellFinder.TradeDropSpot(map);

            // 螺旋搜索空投点
            int maxSearchRadius = 30;
            for (int radius = 0; radius <= maxSearchRadius; radius++)
            {
                // 螺旋搜索当前半径的所有格子
                foreach (IntVec3 cell in GenRadial.RadialCellsAround(targetSpot, radius, true))
                {
                    if (!cell.InBounds(map)) continue;

                    // 检查是否距离地图边缘足够远
                    if (cell.DistanceToEdge(map) < 10) continue;

                    // 检查放置条件
                    if (CanPlaceAt(map, cell, size))
                    {
                        return cell;
                    }
                }
            }

            // 未找到则强制降落
            Log.Warning($"[USAC] FindDropSpotForSize: no suitable spot found for size {size}, using default trade spot");
            return DropCellFinder.TradeDropSpot(map);
        }

        // 检查位置有效性
        private static bool CanPlaceAt(Map map, IntVec3 center, IntVec2 size)
        {
            // 检查所有占用的格子
            foreach (IntVec3 cell in GenAdj.OccupiedRect(center, Rot4.North, size))
            {
                // 基本检查
                if (!cell.InBounds(map)) return false;
                if (cell.Fogged(map)) return false;
                if (!cell.Standable(map)) return false;

                // 检查屋顶
                RoofDef roof = cell.GetRoof(map);
                if (roof != null && roof.isThickRoof) return false;

                // 检查建筑物
                if (cell.GetFirstBuilding(map) != null) return false;

                // 检查其他 Skyfaller
                if (cell.GetFirstSkyfaller(map) != null) return false;

                // 检查 Pawn（避免砸到人）
                if (cell.GetFirstPawn(map) != null) return false;

                // 检查是否有阻止空投的物品
                foreach (Thing thing in cell.GetThingList(map))
                {
                    if (thing.def.preventSkyfallersLandingOn) return false;
                }
            }

            return true;
        }
    }
}
