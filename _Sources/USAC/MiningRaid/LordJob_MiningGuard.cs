using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace USAC
{
    // 采矿护卫群体任务
    // 守卫矿机并执行登机撤离
    // 切换对抗模式但不改变全局关系
    public class LordJob_MiningGuard : LordJob
    {
        #region 字段

        private IntVec3 defendPoint;
        private Building_HeavyMiningRig targetRig;

        #endregion

        #region 属性

        public override bool AddFleeToil => false;

        #endregion

        #region 构造函数

        public LordJob_MiningGuard()
        {
        }

        public LordJob_MiningGuard(IntVec3 point, Building_HeavyMiningRig rig = null)
        {
            defendPoint = point;
            targetRig = rig;
        }

        #endregion

        #region 状态图

        public override StateGraph CreateGraph()
        {
            StateGraph stateGraph = new StateGraph();

            // 防守矿机任务状态
            LordToil_DefendMiningRig toilDefend = new LordToil_DefendMiningRig(defendPoint, defendRadius: 10f, wanderRadius: 5f);
            stateGraph.AddToil(toilDefend);

            // 清理区域威胁状态
            LordToil_KillThreats toilKill = new LordToil_KillThreats(defendPoint, maxChaseRadius: 25f);
            stateGraph.AddToil(toilKill);

            // 登机撤离状态
            LordToil_BoardMiningRig toilBoard = new LordToil_BoardMiningRig(targetRig);
            stateGraph.AddToil(toilBoard);

            // 矿机被毁强制撤离状态
            LordToil_ExitMap toilExit = new LordToil_ExitMap(LocomotionUrgency.Jog);
            stateGraph.AddToil(toilExit);

            // 敌对机器被毁反击状态
            LordToil_AssaultColony toilAssault = new LordToil_AssaultColony(false);
            stateGraph.AddToil(toilAssault);

            // 定义被攻击转化清理威胁逻辑
            Transition transToKill = new Transition(toilDefend, toilKill);
            transToKill.AddTrigger(new Trigger_Memo("StartKillThreats"));
            stateGraph.AddTransition(transToKill);

            // 定义威胁清除转化防守逻辑
            Transition transBackToDefend = new Transition(toilKill, toilDefend);
            transBackToDefend.AddTrigger(new Trigger_Memo("ThreatsCleared"));
            stateGraph.AddTransition(transBackToDefend);

            // 定义防守态转化登机逻辑
            Transition transToBoard = new Transition(toilDefend, toilBoard);
            transToBoard.AddTrigger(new Trigger_Memo("StartBoarding"));
            stateGraph.AddTransition(transToBoard);

            // 定义追猎态转化登机逻辑
            Transition transKillToBoard = new Transition(toilKill, toilBoard);
            transKillToBoard.AddTrigger(new Trigger_Memo("StartBoarding"));
            stateGraph.AddTransition(transKillToBoard);

            // 定义非敌对机器被毁撤退
            Transition transDestroyExit = new Transition(toilDefend, toilExit);
            transDestroyExit.AddSource(toilKill);
            transDestroyExit.AddSource(toilBoard);
            transDestroyExit.AddTrigger(new Trigger_Memo("RigDestroyed_Friendly"));
            stateGraph.AddTransition(transDestroyExit);

            // 定义敌对机器被毁反击
            Transition transDestroyAssault = new Transition(toilDefend, toilAssault);
            transDestroyAssault.AddSource(toilKill);
            transDestroyAssault.AddSource(toilBoard);
            transDestroyAssault.AddTrigger(new Trigger_Memo("RigDestroyed_Hostile"));
            stateGraph.AddTransition(transDestroyAssault);

            // 定义全员登机完成转化结束
            Transition transAllBoarded = new Transition(toilBoard, toilExit);
            transAllBoarded.AddTrigger(new Trigger_Memo("AllBoarded"));
            stateGraph.AddTransition(transAllBoarded);

            return stateGraph;
        }

        #endregion

        #region 存档序列化

        public override void ExposeData()
        {
            Scribe_Values.Look(ref defendPoint, "defendPoint");
            Scribe_References.Look(ref targetRig, "targetRig");
        }

        #endregion

        #region 公共方法

        // 配置关联目标矿机
        public void SetTargetRig(Building_HeavyMiningRig rig)
        {
            targetRig = rig;
        }

        // 发送全员开始登机信号
        public void NotifyStartBoarding()
        {
            // 同步更新登机任务目标
            foreach (LordToil toil in lord.Graph.lordToils)
            {
                if (toil is LordToil_BoardMiningRig boardToil)
                    boardToil.SetTargetRig(targetRig);
            }
            lord.ReceiveMemo("StartBoarding");
        }

        #endregion
    }
}
