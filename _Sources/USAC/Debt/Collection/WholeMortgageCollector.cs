using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace USAC
{
    // 優先抓取抵押建築對象
    public class WholeMortgageCollector : ICollectionStrategy
    {
        public float Execute(Map map, float targetAmount,
            DebtContract contract)
        {
            if (map == null) return 0f;

            float remaining = targetAmount;
            var candidates = BuildCandidateList(map, contract);

            foreach (var t in candidates)
            {
                if (remaining <= 0) break;
                remaining -= t.MarketValue * t.stackCount;
                SpawnGripperForTarget(t, map);
            }

            return targetAmount - remaining;
        }

        #region 候选列表构建
        protected virtual List<Thing> BuildCandidateList(
            Map map, DebtContract contract)
        {
            var result = new List<Thing>();

            // 檢索高價值受控物品區域
            result.AddRange(
                map.listerThings.AllThings
                    .Where(t => t is not Pawn && t is not Building
                        && t.MarketValue * t.stackCount >= 300f
                        && t.Spawned
                        && !t.def.IsCorpse && !t.def.IsBlueprint && !t.def.IsFrame
                        && t.def.defName != "USAC_Bond"
                        && (map.areaManager.Home[t.Position] || t.IsInAnyStorage()))
                    .OrderBy(t => GetRoofPriority(t.Position, map))
                    .ThenByDescending(t => t.MarketValue));

            // 檢索指定低價值建築對象
            result.AddRange(
                map.listerThings.AllThings
                    .OfType<Building>()
                    .Where(b => b.Faction == Faction.OfPlayer
                        && b.MarketValue >= 300f && b.Spawned
                        && !b.def.IsBlueprint && !b.def.IsFrame)
                    .OrderBy(b => GetRoofPriority(b.Position, map))
                    .ThenByDescending(b => b.MarketValue));

            // 檢索合適囚犯與奴隸對象
            result.AddRange(
                map.mapPawns.AllPawnsSpawned
                    .Where(p => p.Faction == Faction.OfPlayer
                        && (p.IsPrisoner || p.IsSlave) && !p.Dead
                        && !IsUnderThickRoof(p.Position, map)));

            // 檢索地圖活動機兵對象
            result.AddRange(
                map.mapPawns.AllPawnsSpawned
                    .Where(p => p.Faction == Faction.OfPlayer
                        && p.RaceProps.IsMechanoid && !p.Dead
                        && !IsUnderThickRoof(p.Position, map)));

            // 判斷並抓取本地殖民者對象
            if (contract.MissedPayments >= 3)
            {
                result.AddRange(
                    map.mapPawns.AllPawnsSpawned
                        .Where(p => p.IsColonist && !p.Dead
                            && !p.IsPrisoner && !p.IsSlave
                            && !IsUnderThickRoof(p.Position, map))
                        .OrderByDescending(p => p.MarketValue));
            }

            return result;
        }
        #endregion

        #region 屋顶辅助
        protected static int GetRoofPriority(IntVec3 c, Map map)
        {
            if (!c.Roofed(map)) return 0;
            if (c.GetRoof(map).isThickRoof) return 2;
            return 1;
        }

        protected static bool IsUnderThickRoof(IntVec3 c, Map map)
        {
            RoofDef roof = c.GetRoof(map);
            return roof != null && roof.isThickRoof;
        }
        #endregion

        #region 夹具派遣
        // 根据目标屋顶情况决定派遣策略
        protected static void SpawnGripperForTarget(Thing target, Map map)
        {
            if (!target.Spawned) return;
            if (IsUnderThickRoof(target.Position, map))
                SpawnDrillThenGripper(target, map);
            else
                SpawnGripper(target, map);
        }

        // 派遣夹具并破拆
        protected static void SpawnGripper(Thing target, Map map)
        {
            var gripper = (Skyfaller_USACGripper)ThingMaker.MakeThing(
                USAC_DefOf.USAC_GripperIncoming);
            gripper.SetTarget(target);
            GenSpawn.Spawn(gripper, target.Position, map);
        }

        // 发射破拆弹及夹具
        protected static void SpawnDrillThenGripper(Thing target, Map map)
        {
            IntVec3 targetPos = target.Position;
            // 说明坐标系轴向
            // 设置俯冲投射起点
            Vector3 origin = targetPos.ToVector3();
            origin.z += 100f; // 高空俯冲定位

            var proj = (Projectile_USACDrillShell)GenSpawn.Spawn(
                USAC_DefOf.USAC_DrillShellProjectile, targetPos, map);

            proj.SetPayload(target);
            // 目标点锁定在物体的实际地面坐标
            proj.Launch(null, origin, targetPos, targetPos, ProjectileHitFlags.IntendedTarget);
        }
        #endregion
    }
}
