using System;
using System.Collections.Generic;
using Verse;

namespace USAC
{
    // 系统缓存工具
    public static class USAC_Cache
    {
        #region 时效缓存

        // 缓存条目结构
        private class CacheEntry<T>
        {
            public T Value;
            public int ExpireTick;
        }

        // 定时缓存映射
        private static readonly Dictionary<string, object> timedCache = new Dictionary<string, object>();

        // 获取或创建缓存
        public static T GetOrCreate<T>(string key, Func<T> creator, int validTicks = 60)
        {
            int currentTick = Find.TickManager?.TicksGame ?? 0;

            if (timedCache.TryGetValue(key, out object cached))
            {
                var entry = cached as CacheEntry<T>;
                if (entry != null && currentTick < entry.ExpireTick)
                {
                    return entry.Value;
                }
            }

            // 创建新缓存对象
            T value = creator();
            timedCache[key] = new CacheEntry<T>
            {
                Value = value,
                ExpireTick = currentTick + validTicks
            };
            return value;
        }

        // 移除指定缓存
        public static void Invalidate(string key)
        {
            timedCache.Remove(key);
        }

        // 移除前缀匹配缓存
        public static void InvalidateByPrefix(string prefix)
        {
            var toRemove = new List<string>();
            foreach (var key in timedCache.Keys)
            {
                if (key.StartsWith(prefix))
                {
                    toRemove.Add(key);
                }
            }
            foreach (var key in toRemove)
            {
                timedCache.Remove(key);
            }
        }

        // 清空全部缓存
        public static void ClearAll()
        {
            timedCache.Clear();
        }

        #endregion

        #region 着色器内核缓存

        // 着色器内核索引
        private static readonly Dictionary<(UnityEngine.ComputeShader, string), int> kernelCache
            = new Dictionary<(UnityEngine.ComputeShader, string), int>();

        // 获取着色器内核
        public static int GetKernel(UnityEngine.ComputeShader shader, string kernelName)
        {
            if (shader == null) return -1;

            var key = (shader, kernelName);
            if (!kernelCache.TryGetValue(key, out int kernelId))
            {
                try
                {
                    kernelId = shader.FindKernel(kernelName);
                }
                catch (Exception ex)
                {
                    Log.ErrorOnce($"[USAC] 着色器内核查找失败 '{kernelName}' in '{shader.name}': {ex.Message}", shader.GetInstanceID() ^ kernelName.GetHashCode());
                    kernelId = -1;
                }
                kernelCache[key] = kernelId;
            }
            return kernelId;
        }

        #endregion

        #region 组件缓存

        // 物体组件引用缓存
        private static readonly Dictionary<(int, Type), object> compCache = new Dictionary<(int, Type), object>();

        // 获取物体组件
        public static T GetComp<T>(ThingWithComps thing) where T : ThingComp
        {
            if (thing == null) return null;

            var key = (thing.thingIDNumber, typeof(T));
            if (!compCache.TryGetValue(key, out object cached))
            {
                cached = thing.GetComp<T>();
                compCache[key] = cached;
            }
            return cached as T;
        }

        // 清理销毁物体缓存
        public static void CleanupDestroyedThings()
        {
            // 清空组件缓存
            compCache.Clear();
        }

        #endregion
    }
}
