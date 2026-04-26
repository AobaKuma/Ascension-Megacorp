using Verse;

namespace USAC
{
    // 地图屋顶检测工具类
    public static class MapRoofUtility
    {
        // 检查位置是否在厚屋顶下
        public static bool IsUnderThickRoof(IntVec3 cell, Map map)
        {
            if (map == null || !cell.InBounds(map)) return false;
            RoofDef roof = cell.GetRoof(map);
            return roof != null && roof.isThickRoof;
        }

        // 检查地图是否完全封闭
        public static bool IsMapSealedFromOrbit(Map map)
        {
            if (map == null) return false;
            return IsFullyRoofed(map) && !HasWalkableBorder(map);
        }

        // 检查地图是否完全覆盖屋顶
        private static bool IsFullyRoofed(Map map)
        {
            var roofGrid = map.roofGrid;
            int total = map.cellIndices.NumGridCells;
            for (int i = 0; i < total; i++)
            {
                if (roofGrid.RoofAt(i) == null)
                    return false;
            }
            return true;
        }

        // 检查地图边界是否有可行走区域
        private static bool HasWalkableBorder(Map map)
        {
            int w = map.Size.x;
            int h = map.Size.z;

            // 检查上下边界
            for (int x = 0; x < w; x++)
            {
                if (new IntVec3(x, 0, 0).Walkable(map)) return true;
                if (new IntVec3(x, 0, h - 1).Walkable(map)) return true;
            }

            // 检查左右边界
            for (int z = 1; z < h - 1; z++)
            {
                if (new IntVec3(0, 0, z).Walkable(map)) return true;
                if (new IntVec3(w - 1, 0, z).Walkable(map)) return true;
            }

            return false;
        }

        // 获取屋顶优先级
        public static int GetRoofPriority(IntVec3 cell, Map map)
        {
            if (!cell.Roofed(map)) return 0;
            if (IsUnderThickRoof(cell, map)) return 2;
            return 1;
        }
    }
}
