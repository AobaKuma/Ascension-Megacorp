using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

public class BuildBundle
{
    private static string ModBundleOutputPath => Path.GetFullPath(Path.Combine(Application.dataPath, "../../1.6/AssetBundles"));

    #region 菜单项

    [MenuItem("Build/Build All Platforms")]
    public static void BuildAll()
    {
        Build("usac_visuals_windows", BuildTarget.StandaloneWindows64);
        Build("usac_visuals_linux", BuildTarget.StandaloneLinux64);
        Build("usac_visuals_mac", BuildTarget.StandaloneOSX);
    }

    [MenuItem("Build/Build Windows Only")]
    public static void BuildWindows()
    {
        Build("usac_visuals_windows", BuildTarget.StandaloneWindows64);
    }

    [MenuItem("Build/Build Linux Only")]
    public static void BuildLinux()
    {
        Build("usac_visuals_linux", BuildTarget.StandaloneLinux64);
    }

    [MenuItem("Build/Build Mac Only")]
    public static void BuildMac()
    {
        Build("usac_visuals_mac", BuildTarget.StandaloneOSX);
    }

    #endregion

    #region 构建逻辑

    private static void Build(string bundleName, BuildTarget target)
    {
        string outputPath = Path.GetFullPath(ModBundleOutputPath);

        if (!Directory.Exists(outputPath))
            Directory.CreateDirectory(outputPath);

        List<string> assets = CollectShaderAssets();

        if (assets.Count == 0)
        {
            Debug.LogError("[BuildBundle] 未找到任何着色器资产！");
            return;
        }

        AssetBundleBuild[] builds = new AssetBundleBuild[1];
        builds[0].assetBundleName = bundleName;
        builds[0].assetNames = assets.ToArray();

        Debug.Log($"[BuildBundle] 开始构建 {bundleName} -> {outputPath}");

        BuildPipeline.BuildAssetBundles(
            outputPath,
            builds,
            BuildAssetBundleOptions.ChunkBasedCompression,
            target
        );

        Debug.Log($"[BuildBundle] 完成 {bundleName}");
    }

    private static List<string> CollectShaderAssets()
    {
        var assets = new List<string>();
        const string shaderDir = "Assets/Shaders";

        if (!Directory.Exists(shaderDir))
        {
            Debug.LogError($"[BuildBundle] 着色器目录不存在: {shaderDir}");
            return assets;
        }

        foreach (string file in Directory.GetFiles(shaderDir, "*.shader"))
        {
            string path = file.Replace("\\", "/");
            assets.Add(path);
            Debug.Log("[BuildBundle] 添加 shader: " + path);
        }
        foreach (string file in Directory.GetFiles(shaderDir, "*.compute"))
        {
            string path = file.Replace("\\", "/");
            assets.Add(path);
            Debug.Log("[BuildBundle] 添加 compute: " + path);
        }

        return assets;
    }

    // 命令行入口
    public static void BuildFromCommandLine()
    {
        BuildAll();
    }

    #endregion
}
