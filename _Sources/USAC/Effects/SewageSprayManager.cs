using UnityEngine;
using Verse;
using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Linq;

namespace USAC
{
    [StaticConstructorOnStartup]
    public class SewageSprayManager : MapComponent
    {
        #region 字段

        private ComputeBuffer particleBuffer;
        private ComputeBuffer argsBuffer;
        private Material instanceMaterial;
        private ComputeShader computeShader;
        private Mesh particleMesh;

        private const int MAX_PARTICLES = 262144;
        private uint[] args = new uint[5] { 0, 0, 0, 0, 0 };

        // 使用有序字典确保索引固定
        private SortedDictionary<int, Vector3> activeSourcesThisTick = new SortedDictionary<int, Vector3>();
        private Vector2 windOffset = Vector2.zero;

        #endregion

        #region 构造函数与注入

        public SewageSprayManager(Map map) : base(map)
        {
        }

        public static void RegisterEmissionSource(Map map, Vector3 pos, int thingID)
        {
            map?.GetComponent<SewageSprayManager>()?.RegisterActiveSource(pos, thingID);
        }

        public void RegisterActiveSource(Vector3 pos, int thingID)
        {
            // 通过 ThingID 锁定槽位
            activeSourcesThisTick[thingID] = pos;
        }

        #endregion

        #region 生命周期

        public override void MapComponentTick()
        {
            base.MapComponentTick();

            if (particleBuffer == null)
            {
                InitializeBuffers();
                if (particleBuffer == null) return;
            }

            UpdateGlobalWind();

            // 统计并同步发射源数据
            int sourceCount = activeSourcesThisTick.Count;
            bool isEmitting = sourceCount > 0;

            // 严格按顺序填充实例数组
            USAC_GlobalEffectManager.ActiveSourceCount = Mathf.Min(sourceCount, 32);
            int idx = 0;
            foreach (var kvp in activeSourcesThisTick)
            {
                if (idx >= 32) break;
                USAC_GlobalEffectManager.EmitterPositions[idx++] = kvp.Value;
            }

            int kernel = USAC_Cache.GetKernel(computeShader, "Update");
            if (kernel >= 0)
            {
                computeShader.SetBuffer(kernel, "particleBuffer", particleBuffer);

                // 物理过采样补偿
                // 提高单步发射率填补空隙
                const int subSteps = 8;
                float stepDt = 0.01666f / subSteps;

                computeShader.SetFloat("deltaTime", stepDt);
                computeShader.SetFloat("time", (float)Find.TickManager.TicksGame / 60f);
                computeShader.SetFloat("isEmitting", isEmitting ? 1.0f : 0.0f);

                // 大幅提升计算产率
                // 亚步模式叠加补偿时间稀释
                float baseRate = 12400f * sourceCount * subSteps;
                computeShader.SetFloat("emitRate", isEmitting ? baseRate : 0f);

                computeShader.SetVector("windOffset", new Vector4(windOffset.x, windOffset.y, 0, 0));

                if (isEmitting)
                {
                    computeShader.SetVector("emitterPositions", USAC_GlobalEffectManager.EmitterPositions[0]); // 废弃字段兼容
                    computeShader.SetVectorArray("emitterPositions", USAC_GlobalEffectManager.EmitterPositions);
                    computeShader.SetInt("emitterCount", USAC_GlobalEffectManager.ActiveSourceCount);
                }

                // 在逻辑步内连续派发模拟任务
                for (int i = 0; i < subSteps; i++)
                {
                    computeShader.Dispatch(kernel, Mathf.CeilToInt(MAX_PARTICLES / 64f), 1, 1);
                }
            }

            // 清理缓存重置注册状态
            activeSourcesThisTick.Clear();
        }

        public override void MapComponentUpdate()
        {
            if (particleBuffer == null || computeShader == null || instanceMaterial == null) return;
            if (Find.CurrentMap != map || WorldRendererUtility.WorldRendered) return; // 校验渲染上下文

            base.MapComponentUpdate();

            instanceMaterial.SetBuffer("particleBuffer", particleBuffer);
            instanceMaterial.SetColor("_Color", new Color(0.65f, 0.62f, 0.58f, 0.75f));

            Vector3 mapCenter = new Vector3(map.Size.x / 2f, 0, map.Size.z / 2f);
            Bounds renderBounds = new Bounds(mapCenter, new Vector3(map.Size.x, 200f, map.Size.z));

            // 执行异步间接渲染
            Graphics.DrawMeshInstancedIndirect(particleMesh, 0, instanceMaterial, renderBounds, argsBuffer);
        }

        public override void MapRemoved()
        {
            base.MapRemoved();
            CleanUp();
        }

        #endregion

        #region 内部逻辑

        private void InitializeBuffers()
        {
            if (particleBuffer != null) return;

            computeShader = USAC_AssetBundleLoader.SewageSprayCompute;
            if (computeShader == null) return;

            if (instanceMaterial == null && USAC_AssetBundleLoader.SewageSprayInstancedShader != null)
            {
                instanceMaterial = new Material(USAC_AssetBundleLoader.SewageSprayInstancedShader);
                instanceMaterial.enableInstancing = true;
                instanceMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                instanceMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                instanceMaterial.SetInt("_ZWrite", 0);
                instanceMaterial.renderQueue = 3000;

                Texture2D tex = CreateSoftParticleTexture();
                instanceMaterial.mainTexture = tex;
                instanceMaterial.SetTexture("_MainTex", tex);
            }

            if (instanceMaterial == null) return;

            particleBuffer = new ComputeBuffer(MAX_PARTICLES, 52);
            argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
            particleMesh = CreateQuadMesh();

            if (particleMesh != null)
            {
                args[0] = (uint)particleMesh.GetIndexCount(0);
                args[1] = (uint)MAX_PARTICLES;
                args[2] = (uint)particleMesh.GetIndexStart(0);
                args[3] = (uint)particleMesh.GetBaseVertex(0);
                args[4] = 0;
                argsBuffer.SetData(args);
            }

            int kernel = USAC_Cache.GetKernel(computeShader, "Init");
            if (kernel >= 0)
            {
                computeShader.SetBuffer(kernel, "particleBuffer", particleBuffer);
                computeShader.SetInt("maxParticles", MAX_PARTICLES);
                computeShader.Dispatch(kernel, Mathf.CeilToInt(MAX_PARTICLES / 64f), 1, 1);
            }
        }

        private void UpdateGlobalWind()
        {
            float gameTime = (float)Find.TickManager.TicksGame / 60f;
            float strength = 5.25f + (Mathf.PerlinNoise(gameTime * 0.1f, 100f) - 0.5f) * 1.5f;
            float side = (Mathf.Sin(gameTime * 0.05f) > 0) ? 1f : -1f;
            float baseAngle = side * 95f;
            float wobble = (Mathf.PerlinNoise(gameTime * 0.3f, 200f) - 0.5f) * 30f;
            float rad = (baseAngle + wobble) * Mathf.Deg2Rad;
            windOffset = new Vector2(Mathf.Sin(rad) * strength, Mathf.Cos(rad) * strength);
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
                    alpha *= alpha;
                    colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetPixels(colors);
            tex.Apply();
            return tex;
        }

        private Mesh CreateQuadMesh()
        {
            Mesh m = new Mesh { name = "SewageParticleQuad" };
            m.vertices = new Vector3[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(-0.5f, 0.5f, 0), new Vector3(0.5f, 0.5f, 0) };
            m.uv = new Vector2[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            m.triangles = new int[] { 0, 2, 1, 2, 3, 1 };
            m.RecalculateBounds();
            return m;
        }

        public void CleanUp()
        {
            particleBuffer?.Release();
            argsBuffer?.Release();
            particleBuffer = null;
            argsBuffer = null;
        }

        #endregion
    }

    public static class USAC_GlobalEffectManager
    {
        public static Vector4[] EmitterPositions = new Vector4[32];
        public static int ActiveSourceCount;
        public static bool IsEmitterActive;
    }
}
