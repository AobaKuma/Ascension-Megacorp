using System;
using RimWorld;
using Verse;

namespace USAC
{
    // 债券操作工具类
    public static class DebtBondOperations
    {
        #region 债券操作
        // 获取信标附近的债券数量
        public static int GetBondCountNearBeacons(Map map)
        {
            if (map == null) return 0;
            int count = 0;
            var buildings = map.listerBuildings.allBuildingsColonist;
            for (int i = 0; i < buildings.Count; i++)
            {
                if (buildings[i] is not Building_OrbitalTradeBeacon beacon) continue;
                foreach (IntVec3 c in beacon.TradeableCells)
                {
                    var bond = c.GetFirstThing(map, USAC_DefOf.USAC_Bond);
                    if (bond != null) count += bond.stackCount;
                }
            }
            return count;
        }

        // 消耗信标附近的债券
        public static void ConsumeBondsNearBeacons(Map map, int count)
        {
            int remaining = count;
            var buildings = map.listerBuildings.allBuildingsColonist;
            for (int i = 0; i < buildings.Count; i++)
            {
                if (buildings[i] is not Building_OrbitalTradeBeacon beacon) continue;
                foreach (IntVec3 c in beacon.TradeableCells)
                {
                    var bond = c.GetFirstThing(map, USAC_DefOf.USAC_Bond);
                    if (bond == null) continue;
                    int take = Math.Min(remaining, bond.stackCount);
                    bond.SplitOff(take).Destroy();
                    remaining -= take;
                    if (remaining <= 0) return;
                }
            }
        }

        // 获取地图上所有债券数量
        public static int GetBondCountOnMap(Map map)
        {
            if (map == null) return 0;
            var bonds = map.listerThings.ThingsOfDef(USAC_DefOf.USAC_Bond);
            int count = 0;
            for (int i = 0; i < bonds.Count; i++)
                count += bonds[i].stackCount;
            return count;
        }

        // 消耗地图上的债券
        public static void ConsumeBonds(Map map, int count)
        {
            int remaining = count;
            foreach (var b in map.listerThings.ThingsOfDef(USAC_DefOf.USAC_Bond))
            {
                int take = Math.Min(remaining, b.stackCount);
                b.SplitOff(take).Destroy();
                remaining -= take;
                if (remaining <= 0) break;
            }
        }
        #endregion
    }
}
