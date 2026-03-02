using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace USAC
{
    // USAC轨道夹具完整生命周期
    // 下降→着陆抓取→上升离开
    public class Skyfaller_USACGripper : Skyfaller
    {
        private Thing targetThing;

        private bool isLifting;
        private int liftTicks;
        private const int LiftDuration = 150;
        // 夹具垂直向上偏置
        private const float GripperOffsetZ = 0.5f;
        // 上升阶段水平锚点
        private Vector3 landedAnchor;
        // 上一帧目标位置
        private Vector3 lastTargetPos;
        // 是否已正常完成
        private bool completedNormally;
        // 位置跳变距离阈值
        private const float WarpThreshold = 5f;

        private Rot4 targetRotation = Rot4.North;
        private float gripperScale = 1.5f;

        public void SetTarget(Thing target)
        {
            targetThing = target;
            if (target != null)
            {
                lastTargetPos = target.DrawPos;

                // 根据目标类型计算夹具缩放
                if (target is Pawn pawn)
                {
                    // 基于体型尺寸缩放
                    gripperScale = Mathf.Max(pawn.BodySize * 1.5f, 1.2f);
                }
                else if (target is Building b)
                {
                    gripperScale = Mathf.Max(b.def.size.x, b.def.size.z) * 1.2f;
                }
                else
                {
                    gripperScale = 1.2f; // 普通物品默认缩放
                }

                targetRotation = target.Rotation;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref targetThing, "targetThing");
            Scribe_Values.Look(ref isLifting, "isLifting");
            Scribe_Values.Look(ref liftTicks, "liftTicks");
            Scribe_Values.Look(ref landedAnchor, "landedAnchor");
            Scribe_Values.Look(ref gripperScale, "gripperScale", 1.5f);
            Scribe_Values.Look(ref targetRotation, "targetRotation", Rot4.North);
        }

        protected override void Impact()
        {
            Map map = Map;

            // 追踪目标实际位置落点
            IntVec3 pos = (targetThing is { Spawned: true })
                ? targetThing.Position
                : Position;

            // 破拆屋顶
            if (pos.Roofed(map))
            {
                var roof = pos.GetRoof(map);
                map.roofGrid.SetRoof(pos, null);
                if (roof.isThickRoof)
                {
                    for (int i = 0; i < 3; i++)
                        FleckMaker.ThrowDustPuff(
                            pos.ToVector3Shifted() + Gen.RandomHorizontalVector(0.5f),
                            map, 1.5f);
                }
            }

            FleckMaker.ThrowDustPuff(pos.ToVector3Shifted(), map, 2.5f);

            if (def.skyfaller.impactSound != null)
                def.skyfaller.impactSound.PlayOneShot(
                    SoundInfo.InMap(new TargetInfo(pos, map)));

            // 抓取目标并锁定锚点
            if (targetThing is { Spawned: true })
            {
                landedAnchor = targetThing.DrawPos;
                targetRotation = targetThing.Rotation;
                targetThing.DeSpawn();
                innerContainer.TryAdd(targetThing);
            }
            else
            {
                landedAnchor = pos.ToVector3Shifted();
            }

            // 进入上升阶段
            isLifting = true;
            hasImpacted = true;
        }

        protected override void Tick()
        {
            if (isLifting)
            {
                liftTicks++;
                if (liftTicks >= LiftDuration)
                {
                    completedNormally = true;
                    innerContainer.ClearAndDestroyContents();
                    Destroy();
                }
                return;
            }

            // 下降检测目标跳变
            if (targetThing is { Spawned: true })
            {
                Vector3 curPos = targetThing.DrawPos;
                float d = (curPos - lastTargetPos).sqrMagnitude;
                if (d > WarpThreshold * WarpThreshold)
                {
                    // 目标丢失直接空夹上升
                    Messages.Message("[USAC] 轨道夹具抓取失败：目标丢失。",
                        MessageTypeDefOf.NeutralEvent);
                    landedAnchor = curPos;
                    isLifting = true;
                    hasImpacted = true;
                    return;
                }
                lastTargetPos = curPos;
            }
            else if (targetThing != null && !targetThing.Spawned && !isLifting)
            {
                // 目标意外消失处理
                landedAnchor = Position.ToVector3Shifted();
                isLifting = true;
                hasImpacted = true;
                return;
            }

            base.Tick();
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (isLifting)
            {
                DrawLifting(drawLoc);
                return;
            }
            // XZ轴追踪目标实时位置
            if (targetThing is { Spawned: true })
            {
                Vector3 basePos = Position.ToVector3Shifted();
                Vector3 offset = drawLoc - basePos;
                Vector3 targetPos = targetThing.DrawPos;

                // 修正绘制轨迹偏移量
                drawLoc.x = targetPos.x + offset.x;
                drawLoc.z = targetPos.z + offset.z;
                drawLoc.y = targetPos.y;
            }

            // 绘制目标缩放图像
            if (Graphic != null)
            {
                Graphic.GetCopy(new Vector2(gripperScale, gripperScale), null).Draw(drawLoc, Rot4.North, this);
            }
        }

        // 基于锚点绘制上升动效
        private void DrawLifting(Vector3 drawLoc)
        {
            float t = (float)liftTicks / LiftDuration;
            float riseZ = t * t * 30f;

            // 以锁定的落点锚点为水平基准
            Vector3 rootPos = landedAnchor;
            rootPos.z += riseZ;

            // 原地渲染消除跳变感
            // 设置目标为直线升空

            // 被抓取目标处于提拉的中轴基准上
            if (innerContainer.Count > 0)
            {
                Thing carried = innerContainer[0];
                Vector3 carryPos = rootPos;
                carryPos.y = Altitudes.AltitudeFor(AltitudeLayer.Pawn);
                DrawCarried(carried, carryPos);
            }

            // 依据比例向上偏移夹具
            Vector3 gripperPos = rootPos;
            gripperPos.z += GripperOffsetZ * (gripperScale / 1.5f);
            gripperPos.y = Altitudes.AltitudeFor(AltitudeLayer.Skyfaller);

            // 绘制夹具本体 (应用缩放)
            if (Graphic != null)
            {
                Graphic.GetCopy(new Vector2(gripperScale, gripperScale), null).Draw(gripperPos, Rot4.North, this);
            }
        }

        // 被摘毁时给产壅增加额外债务
        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            if (!completedNormally
                && mode == DestroyMode.KillFinalize
                && Spawned)
            {
                // 罚金计入最近合同
                var debtComp = GameComponent_USACDebt.Instance;
                var target = debtComp?.NextDueContract;
                if (debtComp != null && target != null)
                {
                    float penalty = 3000f;
                    target.Principal += penalty;
                    target.MissedPayments++;
                    debtComp.AddTransaction(
                        USACTransactionType.Penalty, penalty,
                        "轨道夹具遭摧毁 (损坏赔偿)");
                    Messages.Message(
                        "[USAC] 轨道夹具遭摧毁！" +
                        "额外费用₿3000已记入账单",
                        MessageTypeDefOf.NegativeEvent);
                }
            }
            base.Destroy(mode);
        }

        // 保持原始比例朝向渲染
        private void DrawCarried(Thing carried, Vector3 carryPos)
        {
            if (carried is Pawn pawn)
            {
                try
                {
                    // 执行面向镜头渲染
                    pawn.Drawer.renderer.RenderPawnAt(carryPos, Rot4.South);
                }
                catch
                {
                    // 兼容其他模组渲染拦截
                    pawn.Graphic?.Draw(carryPos, Rot4.South, pawn);
                }
            }
            else
            {
                // 物品和建筑保持被抓取前的朝向
                carried.Graphic?.Draw(carryPos, targetRotation, carried);
            }
        }
    }
}
