using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace USAC
{
    // 优先抓取抵押建筑
    public class BuildingMortgageCollector : ICollectionStrategy
    {
        public float Execute(Map map, float targetAmount,
            DebtContract contract)
        {
            if (map == null) return 0f;

            float remaining = targetAmount;
            var buildings = GetBuildingCandidates(map);

            foreach (var b in buildings)
            {
                if (remaining <= 0) break;
                remaining -= b.MarketValue;
                SpawnGripper(b, map);
            }

            // 建筑不足时抓取高价物品补充
            if (remaining > 0)
            {
                var items = GetItemCandidates(map);
                foreach (var item in items)
                {
                    if (remaining <= 0) break;
                    remaining -= item.MarketValue * item.stackCount;
                    SpawnGripper(item, map);
                }
            }

            return targetAmount - remaining;
        }

        private List<Building> GetBuildingCandidates(Map map)
        {
            return map.listerThings.AllThings
                .OfType<Building>()
                .Where(b => b.Faction == Faction.OfPlayer
                    && b.MarketValue > 0 && b.Spawned
                    && !b.def.IsBlueprint && !b.def.IsFrame)
                .OrderBy(b => GetRoofPri(b.Position, map))
                .ThenByDescending(b => b.MarketValue)
                .ToList();
        }

        private List<Thing> GetItemCandidates(Map map)
        {
            return map.listerThings.AllThings
                .Where(t => t is not Pawn && t is not Building
                    && t.Faction == Faction.OfPlayer
                    && t.MarketValue > 0 && t.Spawned
                    && !t.def.IsCorpse && !t.def.IsBlueprint
                    && !t.def.IsFrame
                    && t.def.defName != "USAC_Bond")
                .OrderByDescending(t => t.MarketValue)
                .ToList();
        }

        private static int GetRoofPri(IntVec3 c, Map map)
        {
            if (!c.Roofed(map)) return 0;
            if (c.GetRoof(map).isThickRoof) return 2;
            return 1;
        }

        private static void SpawnGripper(Thing target, Map map)
        {
            var gripper = (Skyfaller_USACGripper)ThingMaker
                .MakeThing(USAC_DefOf.USAC_GripperIncoming);
            gripper.SetTarget(target);
            GenSpawn.Spawn(gripper, target.Position, map);
        }
    }
}
