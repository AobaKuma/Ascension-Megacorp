using System;
using System.Collections.Generic;
using Verse;

namespace USAC
{
    // 定义系统通用缓存工具类
    public static class USAC_Cache
    {
        #region 时效缓存

        // 定义缓存条目数据结构
        private class CacheEntry<T>
        {
            public T Value;
            public int ExpireTick;
        }

        // 维护定时缓存数据映射
        private static Dictionary<string, object> timedCache = new Dictionary<string, object>();

        // 检索或创建指定缓存条目
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

            // 执行缓存缺失时对象创建
            T value = creator();
            timedCache[key] = new CacheEntry<T>
            {
                Value = value,
                ExpireTick = currentTick + validTicks
            };
            return value;
        }

        // 移除指定键名的缓存条目
        public static void Invalidate(string key)
        {
            timedCache.Remove(key);
        }

        // 移除匹配前缀的所有缓存
        public static void InvalidateByPrefix(string prefix)
        {
            List<string> toRemove = new List<string>();
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

        // 清空定时缓存全量数据
        public static void ClearAll()
        {
            timedCache.Clear();
        }

        #endregion

        #region 计算着色器内核缓存

        // 维护计算着色器核心索引
        private static Dictionary<(UnityEngine.ComputeShader, string), int> kernelCache
            = new Dictionary<(UnityEngine.ComputeShader, string), int>();

        // 检索计算着色器核心标识
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
                    Log.ErrorOnce($"[USAC] Failed to find kernel '{kernelName}' in shader '{shader.name}': {ex.Message}", shader.GetInstanceID() ^ kernelName.GetHashCode());
                    kernelId = -1;
                }
                kernelCache[key] = kernelId;
            }
            return kernelId;
        }

        #endregion

        #region 组件缓存

        // 维护物体组件实例引用
        private static Dictionary<(int, Type), object> compCache = new Dictionary<(int, Type), object>();

        // 检索指定物体的组件引用
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

        // 移除已销毁物体组件缓存
        public static void CleanupDestroyedThings()
        {
            // 执行组件缓存全量清空
            compCache.Clear();
        }

        #endregion
    }
}
