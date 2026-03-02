using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;
using Verse.Sound;

namespace USAC
{
    // 定义采矿机垂直降落器逻辑
    // 落地后生成采矿机及其护卫
    public class Skyfaller_MiningRigIncoming : Skyfaller
    {
        // 记录计划生成的护卫人员名单
        private List<Pawn> guards = new List<Pawn>();

        // 引用该次采矿作业所属派系
        private Faction faction;

        // 关联采矿机租期
        private int leaseTicks = -1;
        private bool autoRenew;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref guards, "guards", LookMode.Reference);
            Scribe_References.Look(ref faction, "faction");
            Scribe_Values.Look(ref leaseTicks, "leaseTicks", -1);
            Scribe_Values.Look(ref autoRenew, "autoRenew", false);

            if (guards == null)
                guards = new List<Pawn>();
        }

        // 配置该突袭组所属护卫名单
        public void SetGuards(List<Pawn> pawns, Faction fac)
        {
            this.faction = fac;
            this.guards = pawns ?? new List<Pawn>();

            // 对象入容器
            // 避落前回收
            // 咕咕又嘎嘎
            foreach (Pawn pawn in guards)
            {
                if (pawn != null)
                {
                    if (pawn.Spawned) pawn.DeSpawn();
                    innerContainer.TryAdd(pawn);
                }
            }
            USAC_Debug.Log($"[USAC] Skyfaller prepared with {innerContainer.Count} guards.");
        }

        protected override void Impact()
        {
            Map map = Map;
            IntVec3 pos = Position;
            USAC_Debug.Log($"[USAC] Skyfaller Impact at {pos}. Container count: {innerContainer.Count}");

            // 执行目标位置采矿机建筑生成
            Building_HeavyMiningRig rig = null;
            ThingDef rigDef = DefDatabase<ThingDef>.GetNamed("USAC_HeavyMiningRig");
            if (rigDef != null)
            {
                rig = (Building_HeavyMiningRig)ThingMaker.MakeThing(rigDef);
                GenSpawn.Spawn(rig, pos, map);

                // 配置新生成建筑的所属派系
                if (faction != null)
                {
                    rig.SetFaction(faction);
                }

                // 传递租赁时长
                if (leaseTicks > 0)
                {
                    rig.SetLease(leaseTicks, autoRenew);
                }
            }

            // 执行护卫人员于区域离散生成
            List<Pawn> spawnedGuards = new List<Pawn>();
            foreach (Thing thing in innerContainer.ToList())
            {
                if (thing is Pawn pawn)
                {
                    // 执行采矿机邻近有效格点生成
                    IntVec3 spawnCell = pos;
                    CellFinder.TryFindRandomCellNear(pos, map, 5, c => c.Standable(map) && !c.Fogged(map), out spawnCell);

                    GenSpawn.Spawn(pawn, spawnCell, map);
                    spawnedGuards.Add(pawn);
                }
            }

            // 执行采矿护卫群体逻辑生成
            if (spawnedGuards.Count > 0 && faction != null)
            {
                // 绑定采矿机实例至群体逻辑
                LordJob_MiningGuard lordJob = new LordJob_MiningGuard(pos, rig);
                Lord lord = LordMaker.MakeNewLord(faction, lordJob, map, spawnedGuards);

                // 回传群体逻辑实例至采矿机
                if (rig != null)
                {
                    rig.SetGuardLord(lord);
                }

                USAC_Debug.Log($"[USAC] Created MiningGuard Lord for Rig {rig?.ThingID} with {spawnedGuards.Count} guards, Lord={lord}");

                // 校验护卫是否绑定至主逻辑
                foreach (Pawn guard in spawnedGuards)
                {
                    Lord guardLord = guard.GetLord();
                    USAC_Debug.Log($"[USAC] Guard {guard.LabelShort} Lord={guardLord}, LordJob={guardLord?.LordJob?.GetType().Name}");
                }
            }

            // 清空降落器内部持有的容器
            innerContainer.Clear();

            // 执行物理落地反馈特效生成
            CellRect cellRect = this.OccupiedRect();
            for (int i = 0; i < cellRect.Area * def.skyfaller.motesPerCell; i++)
            {
                FleckMaker.ThrowDustPuff(cellRect.RandomVector3, map, 2f);
            }

            if (def.skyfaller.cameraShake > 0f && map == Find.CurrentMap)
            {
                Find.CameraDriver.shaker.DoShake(def.skyfaller.cameraShake);
            }

            if (def.skyfaller.impactSound != null)
            {
                def.skyfaller.impactSound.PlayOneShot(SoundInfo.InMap(new TargetInfo(pos, map)));
            }
            Destroy();
        }

        public void SetLease(int ticks, bool auto)
        {
            leaseTicks = ticks;
            autoRenew = auto;
        }
    }

    // 定义采矿机重力撤离器逻辑
    public class Skyfaller_MiningRigLeaving : Skyfaller
    {
        private bool craterSpawned = false;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            // 执行起飞点地面坑洞痕迹生成
            if (!respawningAfterLoad && !craterSpawned)
            {
                SpawnCrater();
                craterSpawned = true;
            }
        }

        private void SpawnCrater()
        {
            ThingDef craterDef = ThingDefOf.CraterMedium;
            if (craterDef == null) return;

            // 锁定矿机基座点并生成痕迹
            IntVec3 craterPos = Position;

            // 校验目标格点弹坑痕迹合法性
            if (craterPos.InBounds(Map) && GenConstruct.CanPlaceBlueprintAt(craterDef, craterPos, Rot4.North, Map).Accepted)
            {
                Thing crater = ThingMaker.MakeThing(craterDef);
                GenSpawn.Spawn(crater, craterPos, Map);
                USAC_Debug.Log($"[USAC] Spawned crater at {craterPos}");
            }
        }

        // 实现重绘方法并锁定正向视角
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            // 执行原始图形绘制并无视旋转
            Graphic.Draw(drawLoc, Rot4.North, this);
            DrawDropSpotShadow();
        }

        protected override void LeaveMap()
        {
            // 销毁容器内容
            innerContainer.ClearAndDestroyContents();
            Destroy();
        }
    }
}
