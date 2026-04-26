using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace USAC
{
    // 抵押建筑收缴策略
    public class WholeMortgageCollector : ICollectionStrategy
    {
        public float Execute(Map map, float targetAmount,
            DebtContract contract)
        {
            if (map == null) return 0f;

            float remaining = targetAmount;
            var candidates = BuildCandidateList(map, contract);

            for (int i = 0; i < candidates.Count && remaining > 0; i++)
            {
                var t = candidates[i];
                remaining -= t.MarketValue * t.stackCount;
                SpawnGripperForTarget(t, map, contract);
            }

            return targetAmount - remaining;
        }

        #region 候选列表构建
        protected virtual List<Thing> BuildCandidateList(
            Map map, DebtContract contract)
        {
            var result = new List<Thing>();

            // 高价值物品区域
            var allThings = map.listerThings.AllThings;
            for (int i = 0; i < allThings.Count; i++)
            {
                var t = allThings[i];
                if (t is Pawn || t is Building) continue;
                if (t.MarketValue * t.stackCount < 300f) continue;
                if (!t.Spawned || t.def.IsCorpse || t.def.IsBlueprint || t.def.IsFrame) continue;
                if (t.def.defName == "USAC_Bond") continue;
                if (!map.areaManager.Home[t.Position] && !t.IsInAnyStorage()) continue;
                
                result.Add(t);
            }

            // 按屋顶优先级和价值排序
            result.Sort((a, b) =>
            {
                int roofCompare = GetRoofPriority(a.Position, map).CompareTo(GetRoofPriority(b.Position, map));
                if (roofCompare != 0) return roofCompare;
                return b.MarketValue.CompareTo(a.MarketValue);
            });

            // 玩家建筑对象
            var buildings = map.listerThings.AllThings;
            var buildingCandidates = new List<Building>();
            for (int i = 0; i < buildings.Count; i++)
            {
                if (buildings[i] is Building b && 
                    b.Faction == Faction.OfPlayer &&
                    b.MarketValue >= 300f && b.Spawned &&
                    !b.def.IsBlueprint && !b.def.IsFrame)
                {
                    buildingCandidates.Add(b);
                }
            }

            buildingCandidates.Sort((a, b) =>
            {
                int roofCompare = GetRoofPriority(a.Position, map).CompareTo(GetRoofPriority(b.Position, map));
                if (roofCompare != 0) return roofCompare;
                return b.MarketValue.CompareTo(a.MarketValue);
            });
            
            result.AddRange(buildingCandidates);

            // 囚犯与奴隶对象
            var allPawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < allPawns.Count; i++)
            {
                var p = allPawns[i];
                if (p.Faction == Faction.OfPlayer &&
                    (p.IsPrisoner || p.IsSlave) && !p.Dead &&
                    !IsUnderThickRoof(p.Position, map))
                {
                    result.Add(p);
                }
            }

            // 机兵对象
            for (int i = 0; i < allPawns.Count; i++)
            {
                var p = allPawns[i];
                if (p.Faction == Faction.OfPlayer &&
                    p.RaceProps.IsMechanoid && !p.Dead &&
                    !IsUnderThickRoof(p.Position, map))
                {
                    result.Add(p);
                }
            }

            // 殖民者对象
            if (contract.MissedPayments >= 3)
            {
                var colonists = new List<Pawn>();
                for (int i = 0; i < allPawns.Count; i++)
                {
                    var p = allPawns[i];
                    if (p.IsColonist && !p.Dead &&
                        !p.IsPrisoner && !p.IsSlave &&
                        !IsUnderThickRoof(p.Position, map))
                    {
                        colonists.Add(p);
                    }
                }
                
                colonists.Sort((a, b) => b.MarketValue.CompareTo(a.MarketValue));
                result.AddRange(colonists);
            }

            return result;
        }
        #endregion

        #region 屋顶辅助
        protected static int GetRoofPriority(IntVec3 c, Map map)
        {
            return MapRoofUtility.GetRoofPriority(c, map);
        }

        protected static bool IsUnderThickRoof(IntVec3 c, Map map)
        {
            return MapRoofUtility.IsUnderThickRoof(c, map);
        }
        #endregion

        #region 夹具派遣
        // 根据屋顶情况派遣策略
        protected static void SpawnGripperForTarget(Thing target, Map map, DebtContract contract)
        {
            if (!target.Spawned) return;
            if (IsUnderThickRoof(target.Position, map))
                SpawnDrillThenGripper(target, map, contract);
            else
                SpawnGripper(target, map, contract);
        }

        // 派遣夹具
        protected static void SpawnGripper(Thing target, Map map, DebtContract contract)
        {
            var gripper = (Skyfaller_USACGripper)ThingMaker.MakeThing(
                USAC_DefOf.USAC_GripperIncoming);
            gripper.SetTarget(target);
            gripper.SetTargetContract(contract);
            GenSpawn.Spawn(gripper, target.Position, map);
        }

        // 发射破拆弹及夹具
        protected static void SpawnDrillThenGripper(Thing target, Map map, DebtContract contract)
        {
            IntVec3 targetPos = target.Position;
            // 俯冲投射起点
            Vector3 origin = targetPos.ToVector3();
            origin.z += 100f; // 高空俯冲

            var proj = (Projectile_USACDrillShell)GenSpawn.Spawn(
                USAC_DefOf.USAC_DrillShellProjectile, targetPos, map);

            proj.SetPayload(target, contract);
            // 目标点锁定
            proj.Launch(null, origin, targetPos, targetPos, ProjectileHitFlags.IntendedTarget);
        }
        #endregion
    }
}
