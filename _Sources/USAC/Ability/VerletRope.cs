using UnityEngine;

namespace USAC
{
    // 低阻尼高惯性物理模拟
    // 模拟重型索高速运动
    public class VerletRope
    {
        public struct Node
        {
            public Vector2 planePos;
            public float height;
            public Vector2 prevPlanePos;
            public float prevHeight;
            public float mass;
        }

        private Node[] nodes;
        private readonly int nodeCount;
        private readonly float maxSegLen;
        private readonly float gravity;
        private readonly int solverIterations;

        // 物理常数重设
        // 低阻力惯性主导
        private const float DRAG_AIR = 0.985f;
        // 地面高摩擦落地停
        private const float DRAG_GROUND = 0.60f;

        public int NodeCount => nodeCount;
        public Node[] Nodes => nodes;

        public VerletRope(int nodes, float totalLength, float grav, int iterations = 30)
        {
            this.nodeCount = nodes;
            this.maxSegLen = totalLength / (nodes - 1);
            this.gravity = grav;
            this.solverIterations = iterations;
            this.nodes = new Node[nodes];
        }

        private Vector2 lateralAxis;

        public void InitFlaked(Vector2 plane, float h, Vector2 direction)
        {
            Vector2 forward = direction.normalized;
            lateralAxis = new Vector2(-forward.y, forward.x); // 存储横轴用于湍流

            Vector2 side = lateralAxis;

            // 保守折叠防止物理爆炸
            float safeWidth = Mathf.Min(0.2f, maxSegLen * 0.4f);
            float safeBack = Mathf.Min(0.02f, maxSegLen * 0.1f);

            for (int i = 0; i < nodeCount; i++)
            {
                float jitterH = Random.Range(-0.01f, 0.01f);
                float sideSign = (i % 2 == 0) ? 1f : -1f; // 简单的左右折叠
                float sideOff = safeWidth * sideSign;
                float backOff = (i / 2) * safeBack;

                nodes[i].planePos = plane + (side * sideOff) - (forward * backOff);
                nodes[i].prevPlanePos = nodes[i].planePos;
                nodes[i].height = h + jitterH;
                nodes[i].prevHeight = nodes[i].height;
            }
        }

        public void Simulate(Vector2 startPlane, float startH, Vector2 endPlane, float endH)
        {
            VerletIntegrate();

            // 强制锚定
            nodes[0].planePos = startPlane;
            nodes[0].height = startH;
            nodes[nodeCount - 1].planePos = endPlane;
            nodes[nodeCount - 1].height = endH;

            for (int iter = 0; iter < solverIterations; iter++)
            {
                ApplyTensionConstraints();

                // 迭代锚定
                nodes[0].planePos = startPlane;
                nodes[0].height = startH;
                nodes[nodeCount - 1].planePos = endPlane;
                nodes[nodeCount - 1].height = endH;
            }

            // 地面碰撞与摩擦
            for (int i = 1; i < nodeCount - 1; i++)
            {
                if (nodes[i].height <= 0.01f)
                {
                    // 落地动能转横向散射
                    if (nodes[i].prevHeight > 0.05f)
                    {
                        float verticalSpeed = nodes[i].prevHeight - nodes[i].height;
                        float scatter = Random.Range(-1.5f, 1.5f) * verticalSpeed;
                        nodes[i].planePos += lateralAxis * scatter;
                        // 更新前帧位置产冲量
                        nodes[i].prevPlanePos += lateralAxis * scatter * 0.5f;
                    }

                    nodes[i].height = 0f;
                    nodes[i].prevHeight = 0f;

                    // 强地面摩擦模拟
                    Vector2 vel = nodes[i].planePos - nodes[i].prevPlanePos;
                    nodes[i].prevPlanePos = nodes[i].planePos - vel * DRAG_GROUND;
                }
            }
        }

        private void VerletIntegrate()
        {
            for (int i = 1; i < nodeCount - 1; i++)
            {
                float drag = (nodes[i].height <= 0.01f) ? DRAG_GROUND : DRAG_AIR;

                Vector2 planePos = nodes[i].planePos;
                float height = nodes[i].height;

                // 标准 Verlet 积分
                Vector2 velP = (planePos - nodes[i].prevPlanePos) * drag;
                float velH = (height - nodes[i].prevHeight) * drag;

                // 施加正弦波湍流力场
                if (height > 0.1f)
                {
                    float wave = Mathf.Sin(Time.time * 3f + i * 0.4f) * 0.003f;
                    velP += lateralAxis * wave;
                }

                nodes[i].prevPlanePos = planePos;
                nodes[i].prevHeight = height;

                nodes[i].planePos += velP;
                nodes[i].height += velH - gravity; // 施加重力加速度
            }
        }

        private void ApplyTensionConstraints()
        {
            for (int i = 0; i < nodeCount - 1; i++)
            {
                Vector2 dPlane = nodes[i + 1].planePos - nodes[i].planePos;
                float dH = nodes[i + 1].height - nodes[i].height;
                // 使用开方保证精度
                float dist = Mathf.Sqrt(dPlane.sqrMagnitude + dH * dH);

                if (dist <= maxSegLen) continue;

                float diff = (dist - maxSegLen) / dist;

                // 质量加权
                float totalM = nodes[i].mass + nodes[i + 1].mass;
                float rA = nodes[i + 1].mass / totalM;
                float rB = nodes[i].mass / totalM;

                Vector2 pushP = dPlane * diff;
                float pushH = dH * diff;

                if (i != 0) // 锚点不动
                {
                    nodes[i].planePos += pushP * rA;
                    nodes[i].height += pushH * rA;
                }
                if (i + 1 != nodeCount - 1)
                {
                    nodes[i + 1].planePos -= pushP * rB;
                    nodes[i + 1].height -= pushH * rB;
                }
            }
        }

        public Vector3 GetVisualPos(int index, float layerY)
        {
            return new Vector3(nodes[index].planePos.x, layerY, nodes[index].planePos.y + nodes[index].height);
        }
    }
}
