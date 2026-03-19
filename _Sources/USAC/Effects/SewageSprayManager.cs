using UnityEngine;
using Verse;
using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;

namespace USAC
{
    [StaticConstructorOnStartup]
    public class SewageSprayManager : MapComponent, System.IDisposable
    {
        #region 字段

        private ComputeBuffer particleBuffer;
        private ComputeBuffer argsBuffer;
        private Material instanceMaterial;
        private ComputeShader computeShader;
        private Mesh particleMesh;

        private const int MAX_PARTICLES = 262144;
        private uint[] args = new uint[5] { 0, 0, 0, 0, 0 };

        // 游戏时间累积器
        private float gameTimeAccumulated;

        // 发射源快照
        private Vector4[] emitterSnapshot = new Vector4[32];
        private int snapshotCount;
        private bool snapshotEmitting;
        private Vector2 windOffset = Vector2.zero;

        // 活跃发射源
        private readonly SortedDictionary<int, Vector3> activeSourcesThisTick = new SortedDictionary<int, Vector3>();

        // GPU降级日志标记
        private static bool gpuFallbackLogged;

        // 资源释放标记
        private bool disposed = false;

        // 粒子活跃度追踪
        private int idleFrameCount = 0;
        private const int MAX_IDLE_FRAMES = 120;
        private bool hasEverEmitted = false;

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

            // 每 Tick 累积游戏时间
            gameTimeAccumulated += 1f / 60f;

            UpdateGlobalWind();

            // 写入发射源快照
            int sourceCount = activeSourcesThisTick.Count;
            snapshotCount = Mathf.Min(sourceCount, 32);
            snapshotEmitting = sourceCount > 0;

            int idx = 0;
            foreach (var kvp in activeSourcesThisTick)
            {
                if (idx >= 32) break;
                emitterSnapshot[idx++] = kvp.Value;
            }

            activeSourcesThisTick.Clear();
        }

        public override void MapComponentUpdate()
        {
            if (particleBuffer == null || computeShader == null || instanceMaterial == null) return;
            if (Find.CurrentMap != map || WorldRendererUtility.WorldRendered) return;

            base.MapComponentUpdate();

            // 活跃度检查优化
            if (!snapshotEmitting)
            {
                idleFrameCount++;
                // 空闲超过2秒且曾经发射过则跳过更新
                if (idleFrameCount > MAX_IDLE_FRAMES && hasEverEmitted)
                {
                    return;
                }
            }
            else
            {
                idleFrameCount = 0;
                hasEverEmitted = true;
            }

            // 消费累积游戏时间
            float dt = gameTimeAccumulated;
            if (dt <= 0f) dt = 0f;
            gameTimeAccumulated = 0f;

            // 每帧执行一次Dispatch
            int kernel = USAC_Cache.GetKernel(computeShader, "Update");
            if (kernel >= 0)
            {
                computeShader.SetBuffer(kernel, "particleBuffer", particleBuffer);
                computeShader.SetFloat("deltaTime", dt);
                computeShader.SetFloat("time", (float)Find.TickManager.TicksGame / 60f);
                computeShader.SetFloat("isEmitting", snapshotEmitting ? 1.0f : 0.0f);

                // 发射率根据游戏时间步长动态缩放
                float baseRate = 12400f * snapshotCount;
                computeShader.SetFloat("emitRate", snapshotEmitting ? baseRate : 0f);

                computeShader.SetVector("windOffset", new Vector4(windOffset.x, windOffset.y, 0, 0));

                if (snapshotEmitting)
                {
                    computeShader.SetVector("emitterPositions", emitterSnapshot[0]);
                    computeShader.SetVectorArray("emitterPositions", emitterSnapshot);
                    computeShader.SetInt("emitterCount", snapshotCount);
                }

                computeShader.Dispatch(kernel, Mathf.CeilToInt(MAX_PARTICLES / 64f), 1, 1);
            }

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
            Dispose();
        }

        // 实现IDisposable接口
        public void Dispose()
        {
            Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed && disposing)
            {
                CleanUp();
                disposed = true;
            }
        }

        // 析构函数确保资源释放
        ~SewageSprayManager()
        {
            Dispose(false);
        }

        #endregion

        #region 内部逻辑

        private void InitializeBuffers()
        {
            if (particleBuffer != null) return;

            computeShader = USAC_AssetBundleLoader.SewageSprayCompute;
            if (computeShader == null)
            {
                if (!gpuFallbackLogged)
                {
                    gpuFallbackLogged = true;
                    Log.Warning("[USAC] GPU 粒子路径降级: ComputeShader 未加载 (AssetBundle 缺失或加载失败), 将使用 CPU Fleck 方案.");
                }
                return;
            }

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

            if (instanceMaterial == null)
            {
                if (!gpuFallbackLogged)
                {
                    gpuFallbackLogged = true;
                    Log.Warning("[USAC] GPU 粒子路径降级: Instanced Shader 未加载或 Material 创建失败, 将使用 CPU Fleck 方案.");
                }
                return;
            }

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
            try
            {
                particleBuffer?.Release();
                argsBuffer?.Release();
                
                if (instanceMaterial != null)
                {
                    UnityEngine.Object.DestroyImmediate(instanceMaterial);
                }
                
                if (particleMesh != null)
                {
                    UnityEngine.Object.DestroyImmediate(particleMesh);
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"[USAC] 清理GPU资源时出错: {ex}");
            }
            finally
            {
                particleBuffer = null;
                argsBuffer = null;
                instanceMaterial = null;
                particleMesh = null;
            }
        }

        #endregion
    }

    public static class USAC_GlobalEffectManager
    {
        public static bool IsEmitterActive;
    }
}
