using UnityEngine;
using Verse;
using RimWorld;
using System.Collections.Generic;
using System.Linq;

namespace USAC
{
    [StaticConstructorOnStartup]
    public class SewageSprayManager : MapComponent
    {
        // 初始化粒子渲染缓存
        private ComputeBuffer particleBuffer;
        private ComputeBuffer argsBuffer;
        private Material instanceMaterial;
        private ComputeShader computeShader;
        private Mesh particleMesh;

        private const int MAX_PARTICLES = 40000;
        private uint[] args = new uint[5] { 0, 0, 0, 0, 0 };

        // 定义粒子系统数据结构
#pragma warning disable CS0649
        struct Particle
        {
            public Vector3 position;
            public Vector3 velocity;
            public float life;
            public float maxLife;
            public float size;
            public Vector3 color;
            public float mass;
        }
#pragma warning restore CS0649

        public SewageSprayManager(Map map) : base(map)
        {
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            // 执行延迟线程资源初始化
        }

        private void InitializeBuffers()
        {
            if (particleBuffer != null) return;

            // 校验渲染资源加载状态
            if (USAC_AssetBundleLoader.SewageSprayCompute == null || USAC_AssetBundleLoader.SewageSprayInstancedShader == null)
            {
                // 执行资源加载补丁尝试
                if (USAC_AssetBundleLoader.IsLoaded)
                {
                    computeShader = USAC_AssetBundleLoader.SewageSprayCompute;
                }

                if (computeShader == null) return;
            }
            else
            {
                computeShader = USAC_AssetBundleLoader.SewageSprayCompute;
            }

            // 创建渲染实例化材质
            if (instanceMaterial == null && USAC_AssetBundleLoader.SewageSprayInstancedShader != null)
            {
                instanceMaterial = new Material(USAC_AssetBundleLoader.SewageSprayInstancedShader);
                instanceMaterial.enableInstancing = true;

                // 配置材质透明混合模式
                instanceMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                instanceMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                instanceMaterial.SetInt("_ZWrite", 0);
                instanceMaterial.renderQueue = 3000;

                // 应用软粒子衰减贴图
                Texture2D softParticleTex = CreateSoftParticleTexture();
                instanceMaterial.mainTexture = softParticleTex;
                instanceMaterial.SetTexture("_MainTex", softParticleTex);
            }

            if (instanceMaterial == null) return;

            // 初始化粒子数据缓冲区
            // 定义 GPU 粒子结构大小
            // 缓冲区步长 52 字节
            particleBuffer = new ComputeBuffer(MAX_PARTICLES, 52);

            // 初始化间接渲染参数
            argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);

            // 创建Quad网格
            if (particleMesh == null) particleMesh = CreateQuadMesh();

            if (particleMesh != null)
            {
                args[0] = (uint)particleMesh.GetIndexCount(0);
                args[1] = (uint)MAX_PARTICLES;
                args[2] = (uint)particleMesh.GetIndexStart(0);
                args[3] = (uint)particleMesh.GetBaseVertex(0);
                args[4] = 0;
                argsBuffer.SetData(args);
            }

            // 执行渲染内核初始化
            int kernel = USAC_Cache.GetKernel(computeShader, "Init");
            if (kernel >= 0)
            {
                computeShader.SetBuffer(kernel, "particleBuffer", particleBuffer);
                computeShader.SetInt("maxParticles", MAX_PARTICLES);
                computeShader.Dispatch(kernel, Mathf.CeilToInt(MAX_PARTICLES / 64f), 1, 1);
            }
        }

        private Texture2D CreateSoftParticleTexture()
        {
            int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] colors = new Color[size * size];
            Vector2 center = new Vector2(size / 2f, size / 2f);
            float maxDist = size / 2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = Mathf.Clamp01(1f - (dist / maxDist));
                    alpha = alpha * alpha; // 平滑衰减
                    colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetPixels(colors);
            tex.Apply();
            return tex;
        }

        private Mesh CreateQuadMesh()
        {
            Mesh m = new Mesh();
            m.name = "SewageParticleQuad";
            m.vertices = new Vector3[] {
                new Vector3(-0.5f, -0.5f, 0),
                new Vector3( 0.5f, -0.5f, 0),
                new Vector3(-0.5f,  0.5f, 0),
                new Vector3( 0.5f,  0.5f, 0)
            };
            m.uv = new Vector2[] {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(0, 1),
                new Vector2(1, 1)
            };
            m.triangles = new int[] { 0, 2, 1, 2, 3, 1 };
            m.RecalculateBounds();
            return m;
        }

        // 记录风向偏移量数据
        private Vector2 windOffset = Vector2.zero;
        private bool wasEmitting = false;

        // 注册多位置同步发射源
        private static Dictionary<Vector3, float> activeSources = new Dictionary<Vector3, float>();
        private const float SOURCE_EXPIRY_TIME = 0.05f;

        public static void RegisterEmissionSource(Vector3 pos)
        {
            activeSources[pos] = Time.realtimeSinceStartup;
        }

        // 计算统一风向系统演化
        private void UpdateGlobalWind()
        {
            // 基础风强
            float strength = 5.25f + (Mathf.PerlinNoise(Time.time * 0.1f, 100f) - 0.5f) * 1.5f;

            // 基础风向夹角
            // 叠加长周期正弦波
            float side = (Mathf.Sin(Time.time * 0.05f) > 0) ? 1f : -1f;
            float baseAngle = side * 95f;
            float wobble = (Mathf.PerlinNoise(Time.time * 0.3f, 200f) - 0.5f) * 30f;

            float finalAngle = baseAngle + wobble;
            float rad = finalAngle * Mathf.Deg2Rad;

            windOffset = new Vector2(Mathf.Sin(rad) * strength, Mathf.Cos(rad) * strength);
        }

        public override void MapComponentUpdate()
        {
            UpdateGlobalWind();

            // 清理过期并收集活跃源
            float currentTime = Time.realtimeSinceStartup;
            List<Vector3> currentActivePos = activeSources
                .Where(kvp => currentTime - kvp.Value < SOURCE_EXPIRY_TIME)
                .Select(kvp => kvp.Key)
                .ToList();

            // 统计当前活跃发射源数
            int sourceCount = currentActivePos.Count;
            USAC_GlobalEffectManager.ActiveSourceCount = Mathf.Min(sourceCount, 8);
            for (int i = 0; i < USAC_GlobalEffectManager.ActiveSourceCount; i++)
            {
                USAC_GlobalEffectManager.EmitterPositions[i] = currentActivePos[i];
            }

            // 刷新粒子发射逻辑状态
            bool isCurrentlyEmitting = sourceCount > 0;
            USAC_GlobalEffectManager.IsEmitterActive = isCurrentlyEmitting;

            base.MapComponentUpdate();

            // Lazy Init
            if (particleBuffer == null)
            {
                InitializeBuffers();
                if (particleBuffer == null) return;
            }

            if (computeShader == null || instanceMaterial == null) return;

            // 执行渲染内核模拟逻辑
            int kernel = USAC_Cache.GetKernel(computeShader, "Update");
            if (kernel >= 0)
            {
                computeShader.SetBuffer(kernel, "particleBuffer", particleBuffer);

                float timeScale = Find.TickManager?.TickRateMultiplier ?? 1f;
                computeShader.SetFloat("deltaTime", Time.deltaTime * timeScale);
                computeShader.SetFloat("time", Time.time);

                computeShader.SetFloat("isEmitting", isCurrentlyEmitting ? 1.0f : 0.0f);
                // 调整粒子发射均匀速率
                computeShader.SetFloat("emitRate", isCurrentlyEmitting ? 12000f : 0f);

                // 同步全局演化风向数据
                computeShader.SetVector("windOffset", new Vector4(windOffset.x, windOffset.y, 0, 0));

                if (isCurrentlyEmitting)
                {
                    computeShader.SetVector("emitterPos", USAC_GlobalEffectManager.EmitterPositions[0]); // 作为剥离逻辑的参考根部
                    computeShader.SetVectorArray("emitterPositions", USAC_GlobalEffectManager.EmitterPositions);
                    computeShader.SetInt("emitterCount", USAC_GlobalEffectManager.ActiveSourceCount);
                }

                computeShader.Dispatch(kernel, Mathf.CeilToInt(MAX_PARTICLES / 64f), 1, 1);
            }
            wasEmitting = isCurrentlyEmitting;

            // 执行实例化间接渲染
            instanceMaterial.SetBuffer("particleBuffer", particleBuffer);
            instanceMaterial.SetColor("_Color", new Color(0.65f, 0.62f, 0.58f, 0.75f));

            Vector3 mapCenter = new Vector3(map.Size.x / 2f, 0, map.Size.z / 2f);
            Bounds renderBounds = new Bounds(mapCenter, new Vector3(map.Size.x, 200f, map.Size.z));

            Graphics.DrawMeshInstancedIndirect(
                particleMesh,
                0,
                instanceMaterial,
                renderBounds,
                argsBuffer
            );


        }

        public void CleanUp()
        {
            particleBuffer?.Release();
            argsBuffer?.Release();
        }

        public override void MapRemoved()
        {
            base.MapRemoved();
            CleanUp();
        }
    }

    // 记录全局渲染状态数据
    public static class USAC_GlobalEffectManager
    {
        public static Vector4[] EmitterPositions = new Vector4[8]; // 最多支持8个同时喷发
        public static int ActiveSourceCount;
        public static bool IsEmitterActive;
    }
}
