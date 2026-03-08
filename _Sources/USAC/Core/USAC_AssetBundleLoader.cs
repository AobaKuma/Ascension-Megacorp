using System;
using UnityEngine;
using Verse;
using RimWorld;

namespace USAC
{
    [StaticConstructorOnStartup]
    public static class USAC_AssetBundleLoader
    {
        #region 字段

        public static Shader SewageSprayShader;
        public static ComputeShader SewageSprayCompute;
        public static Shader SewageSprayInstancedShader;

        public static bool IsLoaded;

        #endregion

        #region 初始化

        static USAC_AssetBundleLoader()
        {
            LongEventHandler.ExecuteWhenFinished(LoadAssetBundle);
        }

        private static void LoadAssetBundle()
        {
            try
            {
                // 根据当前运行平台选择对应 bundle 文件名
                string bundleName = GetPlatformBundleName();
                AssetBundle bundle = FindBundle(bundleName);

                // 向下兼容旧版单包命名
                if (bundle == null)
                    bundle = FindBundle("usac_visuals");

                if (bundle == null)
                {
                    Log.Warning($"[USAC] 未找到 AssetBundle '{bundleName}', 污水特效将降级为 CPU 方案.");
                    return;
                }

                SewageSprayShader = bundle.LoadAsset<Shader>("Assets/Shaders/USAC_SewageSpray.shader");
                SewageSprayCompute = bundle.LoadAsset<ComputeShader>("Assets/Shaders/USAC_SewageSpray_Compute.compute");
                SewageSprayInstancedShader = bundle.LoadAsset<Shader>("Assets/Shaders/USAC_SewageSpray_Instanced.shader");

                // 备选短名称加载
                if (SewageSprayShader == null)
                    SewageSprayShader = bundle.LoadAsset<Shader>("USAC_SewageSpray");
                if (SewageSprayCompute == null)
                    SewageSprayCompute = bundle.LoadAsset<ComputeShader>("USAC_SewageSpray_Compute");
                if (SewageSprayInstancedShader == null)
                    SewageSprayInstancedShader = bundle.LoadAsset<Shader>("USAC_SewageSpray_Instanced");

                if (SewageSprayShader != null && SewageSprayCompute != null && SewageSprayInstancedShader != null)
                {
                    USAC_Debug.Log("[USAC] 成功加载 GPU 粒子系统 shaders.");
                    IsLoaded = true;
                }
                else
                {
                    Log.Warning("[USAC] GPU 着色器加载不完整，将降级为 CPU Fleck 方案. 包内资产列表:");
                    foreach (var name in bundle.GetAllAssetNames())
                        Log.Warning(" - " + name);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[USAC] 加载 AssetBundle 时发生异常: {ex}");
            }
        }

        #endregion

        #region 工具方法

        // 返回当前平台对应的 bundle 文件名
        private static string GetPlatformBundleName()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.LinuxPlayer:
                case RuntimePlatform.LinuxEditor:
                    return "usac_visuals_linux";
                case RuntimePlatform.OSXPlayer:
                case RuntimePlatform.OSXEditor:
                    return "usac_visuals_mac";
                default:
                    return "usac_visuals_windows";
            }
        }

        private static AssetBundle FindBundle(string bundleName)
        {
            foreach (ModContentPack mod in LoadedModManager.RunningModsListForReading)
            {
                if (mod.PackageId.ToLower().Contains("usac") || mod.Name.Contains("USAC"))
                {
                    foreach (AssetBundle bundle in mod.assetBundles.loadedAssetBundles)
                    {
                        if (bundle.name == bundleName) return bundle;
                    }
                }
            }
            return null;
        }

        #endregion
    }
}
