using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace USAC
{
    // 定义重型采矿机建筑类
    public partial class Building_HeavyMiningRig : Building, IThingHolder
    {
        #region 字段

        // 记录当前挖掘进度数值
        private float portionProgress;

        // 记录当前产出收益比率
        private float portionYieldPct;

        // 记录撤离逻辑剩余时长
        private int extractionCountdown = -1;

        // 记录已采集矿物映射数据
        private Dictionary<ThingDef, int> storedMinerals = new Dictionary<ThingDef, int>();

        // 记录资源扫描完成状态
        private bool hasScanned;

        // 记录区域资源存续状态
        private bool hasResources;

        // 记录当前挖掘目标点坐标
        private IntVec3 currentMiningCell = IntVec3.Invalid;

        // 定义舱内守卫存储容器
        private ThingOwner innerContainer;

        // 引用关联守卫主控逻辑
        private Lord guardLord;

        // 记录粒子喷射剩余时长
        private int sprayTicksLeft;

        // 租赁剩余时长
        private int leaseTicksLeft = -1;

        // 开启自动续费
        public bool autoRenew;
        #endregion

        #region 常量

        // 记录挖掘作业覆盖半径
        private const int MiningRadius = 6;

        // 记录单次产出所需工作量
        private const float WorkPerPortion = 10000f;

        // 记录每帧执行工作量常数
        private const float WorkPerTick = 2.44f;

        // 记录撤离动画持续时长
        private const int ExtractionTicks = 2500;

        // 记录粒子喷射时间间隔
        private const int SprayInterval = 60;

        // 记录单次喷射粒子总数
        private const int SprayParticleCount = 8;

        #endregion

        #region 属性

        public override AcceptanceReport ClaimableBy(Faction by) => false;

        public float ProgressToNextPortionPercent => portionProgress / WorkPerPortion;

        public int StoredMineralCount
        {
            get
            {
                int total = 0;
                foreach (var kvp in storedMinerals)
                {
                    total += kvp.Value;
                }
                return total;
            }
        }

        public ThingOwner GetDirectlyHeldThings() => innerContainer;

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }

        #endregion

        #region 构造函数

        public Building_HeavyMiningRig()
        {
            innerContainer = new ThingOwner<Thing>(this);
        }

        #endregion

        #region 生命周期

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            if (!respawningAfterLoad)
            {
                ScanForResources();

                // 延迟资源耗尽撤离
                if (!hasResources && leaseTicksLeft <= 0)
                {
                    StartExtraction();
                }
            }
        }

        public override void PreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            base.PreApplyDamage(ref dinfo, out absorbed);

            Faction attackerFaction = dinfo.Instigator?.Faction;
            if (attackerFaction == null || Faction == null) return;

            // 执行玩家攻击敌对逻辑
            if (attackerFaction == Faction.OfPlayer && !Faction.HostileTo(Faction.OfPlayer))
            {
                Faction.OfPlayer.TryAffectGoodwillWith(Faction, Faction.OfPlayer.GoodwillToMakeHostile(Faction),
                    canSendMessage: true, canSendHostilityLetter: true, HistoryEventDefOf.AttackedBuilding, this);
            }
            // 执行三方攻击关系扣除
            else if (attackerFaction != Faction.OfPlayer && attackerFaction != Faction)
            {
                int goodwillChange = -5;
                Faction.TryAffectGoodwillWith(Faction.OfPlayer, goodwillChange,
                    canSendMessage: false, canSendHostilityLetter: false);
                USAC_Debug.Log($"[USAC] Mining rig attacked by {attackerFaction.Name}, goodwill with player changed by {goodwillChange}");
            }
        }

        protected override void Tick()
        {
            base.Tick();

            if (!Spawned) return;

            // 检测并执行撤离倒计时
            if (extractionCountdown > 0)
            {
                extractionCountdown--;
                if (extractionCountdown <= 0)
                {
                    DoExtraction();
                }
                // 撤离阶段停止粒子喷射
                sprayTicksLeft = 0;
            }
            else
            {
                // 检测租赁到期撤离点
                if (leaseTicksLeft > 0)
                {
                    leaseTicksLeft--;
                    if (leaseTicksLeft == 2500 && autoRenew) TryAutoRenewLease();
                    if (leaseTicksLeft <= 0) StartExtraction();
                }

                // 执行常规资源挖掘逻辑
                if (hasResources)
                {
                    DoMiningWork();
                }
            }

            // 刷新粒子喷射计时状态
            if (sprayTicksLeft > 0)
            {
                sprayTicksLeft--;
                if (Map != null)
                {
                    // 计算粒子发射点空间坐标
                    Vector3 pos = this.DrawPos;
                    pos.y = AltitudeLayer.MoteOverhead.AltitudeFor();
                    pos.x += 0.65f;
                    pos.z += 4.5f;

                    // 注册至全局效果管理器
                    SewageSprayManager.RegisterEmissionSource(Map, pos, thingIDNumber);
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref portionProgress, "portionProgress");
            Scribe_Values.Look(ref portionYieldPct, "portionYieldPct");
            Scribe_Values.Look(ref extractionCountdown, "extractionCountdown", -1);
            Scribe_Values.Look(ref hasScanned, "hasScanned");
            Scribe_Values.Look(ref hasResources, "hasResources");
            Scribe_Values.Look(ref currentMiningCell, "currentMiningCell", IntVec3.Invalid);
            Scribe_Values.Look(ref leaseTicksLeft, "leaseTicksLeft", -1);
            Scribe_Values.Look(ref autoRenew, "autoRenew", false);
            Scribe_Collections.Look(ref storedMinerals, "storedMinerals", LookMode.Def, LookMode.Value);
            Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
            Scribe_References.Look(ref guardLord, "guardLord");

            if (storedMinerals == null)
                storedMinerals = new Dictionary<ThingDef, int>();
            if (innerContainer == null)
                innerContainer = new ThingOwner<Thing>(this);
        }

        public override void Destroy(DestroyMode mode)
        {
            // 执行销毁遗留物掉落
            if (mode == DestroyMode.KillFinalize || mode == DestroyMode.Deconstruct)
            {
                EjectStoredMinerals();
                EjectAllContents();
            }

            base.Destroy(mode);
        }

        #endregion

        #region 登机逻辑

        // 配置关联守卫主控实例
        public void SetGuardLord(Lord lord)
        {
            guardLord = lord;
        }

        // 执行守卫进入容器逻辑
        public bool TryAcceptPawn(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || !pawn.Spawned)
            {
                USAC_Debug.Log($"[USAC] TryAcceptPawn failed: pawn={pawn?.LabelShort}, Dead={pawn?.Dead}, Spawned={pawn?.Spawned}");
                return false;
            }

            USAC_Debug.Log($"[USAC] TryAcceptPawn: {pawn.LabelShort}, container count before={innerContainer.Count}");
            pawn.DeSpawn();
            bool result = innerContainer.TryAdd(pawn);
            USAC_Debug.Log($"[USAC] TryAcceptPawn result: {result}, container count after={innerContainer.Count}");
            return result;
        }

        // 弹出容器内容物
        private void EjectAllContents()
        {
            if (!Spawned || Map == null) return;

            innerContainer.TryDropAll(Position, Map, ThingPlaceMode.Near);
        }

        #endregion

        #region 挖掘逻辑

        private void ScanForResources()
        {
            hasScanned = true;
            hasResources = GetNextResource(out _, out _, out currentMiningCell);
        }

        private bool GetNextResource(out ThingDef resDef, out int countPresent, out IntVec3 cell)
        {
            // 检索圆形区域深层矿物
            int radiusSq = MiningRadius * MiningRadius;
            foreach (IntVec3 c in CellRect.CenteredOn(Position, MiningRadius))
            {
                if (!c.InBounds(Map) || c.DistanceToSquared(Position) > radiusSq) continue;

                ThingDef mineralDef = Map.deepResourceGrid.ThingDefAt(c);
                if (mineralDef != null)
                {
                    int count = Map.deepResourceGrid.CountAt(c);
                    if (count > 0)
                    {
                        resDef = mineralDef;
                        countPresent = count;
                        cell = c;
                        return true;
                    }
                }
            }

            resDef = null;
            countPresent = 0;
            cell = IntVec3.Invalid;
            return false;
        }

        private Dictionary<ThingDef, int> GetAllResourcesInRange()
        {
            Dictionary<ThingDef, int> resources = new Dictionary<ThingDef, int>();
            int radiusSq = MiningRadius * MiningRadius;
            foreach (IntVec3 c in CellRect.CenteredOn(Position, MiningRadius))
            {
                if (!c.InBounds(Map) || c.DistanceToSquared(Position) > radiusSq) continue;

                ThingDef mineralDef = Map.deepResourceGrid.ThingDefAt(c);
                if (mineralDef != null)
                {
                    int count = Map.deepResourceGrid.CountAt(c);
                    if (count > 0)
                    {
                        resources.TryGetValue(mineralDef, out int existing);
                        resources[mineralDef] = existing + count;
                    }
                }
            }

            return resources;
        }

        private void DoMiningWork()
        {
            // 累计当前挖掘工作进度
            portionProgress += WorkPerTick;
            portionYieldPct += WorkPerTick / WorkPerPortion;

            // 执行挖掘完成反馈效果
            if (portionProgress >= WorkPerPortion)
            {
                SprayWastewater();
                TryProducePortion(portionYieldPct);
                portionProgress = 0f;
                portionYieldPct = 0f;
            }
        }

        private void TryProducePortion(float yieldPct)
        {
            ThingDef resDef;
            int countPresent;
            IntVec3 cell;

            if (!GetNextResource(out resDef, out countPresent, out cell))
            {
                // 资源耗尽处理
                hasResources = false;
                currentMiningCell = IntVec3.Invalid;
                if (leaseTicksLeft <= 0) StartExtraction();
                return;
            }

            // 同步当前活跃挖掘坐标
            currentMiningCell = cell;

            // 计算执行采矿产出数量
            int portionCount = Mathf.Min(countPresent, resDef.deepCountPerPortion * 2);
            int yieldCount = Mathf.Max(1, GenMath.RoundRandom(portionCount * yieldPct));

            // 扣除地图深层资源格数值
            Map.deepResourceGrid.SetAt(cell, resDef, countPresent - portionCount);

            // 存入机内矿物存储字典
            storedMinerals.TryGetValue(resDef, out int existing);
            storedMinerals[resDef] = existing + yieldCount;

            // 周期性执行污染扩散逻辑
            SpreadPollution();

            if (!GetNextResource(out _, out _, out currentMiningCell))
            {
                hasResources = false;
                currentMiningCell = IntVec3.Invalid;
                if (leaseTicksLeft <= 0) StartExtraction();
            }
        }

        // 执行区域毒性污染扩散
        private void SpreadPollution()
        {
            if (!ModsConfig.BiotechActive || Map == null) return;

            // 确定受污染目标格点
            int cellsToPollute = 3;
            int polluted = 0;
            int num = GenRadial.NumCellsInRadius(MiningRadius);

            for (int i = 0; i < num && polluted < cellsToPollute; i++)
            {
                IntVec3 cell = Position + GenRadial.RadialPattern[i];
                if (cell.InBounds(Map) && cell.CanPollute(Map))
                {
                    cell.Pollute(Map);
                    polluted++;
                }
            }
        }

        // 执行采矿污水喷射视觉
        private void SprayWastewater()
        {
            if (Map == null) return;

            // 启动全局粒子喷射计时
            sprayTicksLeft = 90;

            // 生成随机离散水滴特效
            if (Rand.Chance(0.1f))
            {
                Vector3 center = this.TrueCenter();
                Vector3 dropletPos = center + new Vector3(
                    Rand.Range(-1.5f, 1.5f),
                    0f,
                    Rand.Range(2f, 5f)
                );

                if (dropletPos.ToIntVec3().ShouldSpawnMotesAt(Map))
                {
                    FleckCreationData dropletData = FleckMaker.GetDataStatic(
                        dropletPos,
                        Map,
                        USAC_DefOf.USAC_WastewaterDroplet,
                        Rand.Range(0.3f, 0.6f)
                    );

                    dropletData.velocityAngle = Rand.Range(0f, 360f);
                    dropletData.velocitySpeed = Rand.Range(0.5f, 1.5f);

                    Map.flecks.CreateFleck(dropletData);
                }
            }
        }

        private void EjectStoredMinerals()
        {
            if (!Spawned || Map == null) return;

            foreach (var kvp in storedMinerals)
            {
                ThingDef mineralDef = kvp.Key;
                int totalCount = kvp.Value;

                while (totalCount > 0)
                {
                    int stackCount = Mathf.Min(totalCount, mineralDef.stackLimit);
                    Thing mineral = ThingMaker.MakeThing(mineralDef);
                    mineral.stackCount = stackCount;

                    IntVec3 spawnCell;
                    if (!CellFinder.TryFindRandomCellNear(Position, Map, 5, c => c.Standable(Map), out spawnCell))
                    {
                        spawnCell = Position;
                    }

                    GenPlace.TryPlaceThing(mineral, spawnCell, Map, ThingPlaceMode.Near);
                    totalCount -= stackCount;
                }
            }

            storedMinerals.Clear();
        }

        private void StartExtraction()
        {
            if (extractionCountdown > 0) return;

            extractionCountdown = ExtractionTicks;
            Messages.Message("USAC_MiningRigExtracting".Translate(), this, MessageTypeDefOf.NeutralEvent);

            // 发送守卫全员登机通知
            NotifyGuardsToBoard();
        }

        // 调用守卫作业主控接口
        private void NotifyGuardsToBoard()
        {
            if (guardLord == null || guardLord.Map == null) return;

            if (guardLord.LordJob is LordJob_MiningGuard miningGuardJob)
            {
                miningGuardJob.SetTargetRig(this);
                miningGuardJob.NotifyStartBoarding();
            }
        }

        private void DoExtraction()
        {
            if (!Spawned) return;

            Map map = Map;
            IntVec3 pos = Position;

            storedMinerals.Clear();

            // 执行撤离飞行物生成逻辑
            ThingDef leavingDef = DefDatabase<ThingDef>.GetNamed("USAC_MiningRigLeaving");
            if (leavingDef != null)
            {
                Skyfaller_MiningRigLeaving skyfaller = (Skyfaller_MiningRigLeaving)ThingMaker.MakeThing(leavingDef);

                // 转移守备力量至降落器
                foreach (Thing thing in innerContainer.ToList())
                {
                    innerContainer.Remove(thing);
                    skyfaller.innerContainer.TryAdd(thing);
                }

                Destroy(DestroyMode.Vanish);
                GenSpawn.Spawn(skyfaller, pos, map);
            }
            else
            {
                FleckMaker.ThrowSmoke(DrawPos, map, 3f);
                Destroy(DestroyMode.Vanish);
            }
        }

        #endregion
        #region 选中时显示

        public override void DrawExtraSelectionOverlays()
        {
            base.DrawExtraSelectionOverlays();

            // 绘制采矿作业有效半径
            GenDraw.DrawRadiusRing(Position, MiningRadius + 0.5f);

            // 同步显示深层资源网格
            if (Map != null)
            {
                Map.deepResourceGrid.MarkForDraw();
            }

            // 高亮显示当前挖掘格点
            if (currentMiningCell.IsValid && currentMiningCell.InBounds(Map))
            {
                GenDraw.DrawFieldEdges(new List<IntVec3> { currentMiningCell }, Color.yellow);
            }
        }

        #endregion

        #region UI

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            // 手动清空已采矿物
            yield return new Command_Action
            {
                defaultLabel = "USAC.UI.Drill.EmptyMinerals".Translate(),
                defaultDesc = "USAC.UI.Drill.EmptyMinerals.Desc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Designators/PickUp", true),
                action = delegate
                {
                    EjectStoredMinerals();
                }
            };

            // 执行开发环境瞬间采矿
            if (DebugSettings.ShowDevGizmos)
            {
                yield return new Command_Action
                {
                    defaultLabel = "DEV: Mine once",
                    action = delegate
                    {
                        SprayWastewater();
                        TryProducePortion(1.0f);
                    }
                };

                yield return new Command_Action
                {
                    defaultLabel = "DEV: Mine all",
                    action = delegate
                    {
                        DevMineAll();
                    }
                };
            }
        }

        private void DevMineAll()
        {
            int radiusSq = MiningRadius * MiningRadius;
            foreach (IntVec3 c in CellRect.CenteredOn(Position, MiningRadius))
            {
                if (!c.InBounds(Map) || c.DistanceToSquared(Position) > radiusSq) continue;

                ThingDef mineralDef = Map.deepResourceGrid.ThingDefAt(c);
                if (mineralDef != null)
                {
                    int count = Map.deepResourceGrid.CountAt(c);
                    if (count > 0)
                    {
                        // 存储矿物
                        storedMinerals.TryGetValue(mineralDef, out int existing);
                        storedMinerals[mineralDef] = existing + count;

                        // 移除指定格资源定义
                        Map.deepResourceGrid.SetAt(c, null, 0);
                    }
                }
            }

            // 刷新资源耗尽后逻辑状态
            hasResources = false;
            currentMiningCell = IntVec3.Invalid;
            portionProgress = 0f;
            portionYieldPct = 0f;

            // 强制启动全员撤离逻辑
            leaseTicksLeft = 0;
            StartExtraction();
        }

        public override string GetInspectString()
        {
            string text = base.GetInspectString();

            if (hasScanned)
            {
                // 缓存检索范围内矿物类型
                string cacheKey = $"MiningRig_{thingIDNumber}_Resources";
                Dictionary<ThingDef, int> allResources = USAC_Cache.GetOrCreate(
                    cacheKey,
                    () => GetAllResourcesInRange(),
                    60
                );
                if (allResources.Count > 0)
                {
                    if (!string.IsNullOrEmpty(text)) text += "\n";

                    List<string> resStrings = new List<string>();
                    foreach (var kvp in allResources)
                    {
                        // 估算预估采矿剩余时长
                        int countPerPortion = kvp.Key.deepCountPerPortion;
                        if (countPerPortion <= 0) countPerPortion = 75;

                        float portions = (float)kvp.Value / countPerPortion;
                        int estTicks = (int)(portions * (WorkPerPortion / WorkPerTick));

                        resStrings.Add($"{kvp.Key.LabelCap}: {kvp.Value} ({estTicks.ToStringTicksToPeriod(allowSeconds: false)})");
                    }

                    text += "USAC_ResourcesInRange".Translate() + ": " + string.Join(", ", resStrings);
                }
                else
                {
                    if (!string.IsNullOrEmpty(text)) text += "\n";
                    text += "USAC_NoResourcesInRange".Translate();
                }

                // 拼接当前挖掘状态文本
                if (currentMiningCell.IsValid && currentMiningCell.InBounds(Map))
                {
                    ThingDef currentRes = Map.deepResourceGrid.ThingDefAt(currentMiningCell);
                    if (currentRes != null)
                    {
                        text += "\n" + "USAC_MiningTarget".Translate(currentRes.LabelCap);
                        text += "\n" + "USAC_MiningPortionProgress".Translate(ProgressToNextPortionPercent.ToStringPercent("F0"));
                    }
                }

                // 拼接已存矿物资源文本
                if (storedMinerals.Count > 0)
                {
                    List<string> storedStrings = new List<string>();
                    foreach (var kvp in storedMinerals)
                    {
                        storedStrings.Add($"{kvp.Key.LabelCap}: {kvp.Value}");
                    }
                    text += "\n" + "USAC_StoredMineralsHeader".Translate() + ": " + string.Join(", ", storedStrings);
                }
            }

            if (extractionCountdown > 0)
            {
                if (!string.IsNullOrEmpty(text)) text += "\n";
                text += "USAC_ExtractionCountdown".Translate(extractionCountdown.ToStringTicksToPeriod());
            }

            if (leaseTicksLeft > 0)
            {
                if (!string.IsNullOrEmpty(text)) text += "\n";
                text += "USAC_LeaseDurationLeft".Translate(leaseTicksLeft.ToStringTicksToPeriod());
            }

            return text;
        }

        public void SetLease(int ticks, bool auto)
        {
            leaseTicksLeft = ticks;
            autoRenew = auto;
        }

        private void TryAutoRenewLease()
        {
            var debtComp = GameComponent_USACDebt.Instance;
            if (debtComp != null && debtComp.GetBondCountNearBeacons(Map) >= 1)
            {
                debtComp.ConsumeBondsNearBeacons(Map, 1);
                leaseTicksLeft += 60000; // 延长1天
                Messages.Message("USAC.Message.LeaseAutoRenewed".Translate(), this, MessageTypeDefOf.PositiveEvent);
            }
            else
            {
                autoRenew = false;
                Messages.Message("USAC.Message.LeaseAutoRenewFailed".Translate(), this, MessageTypeDefOf.NegativeEvent);
            }
        }

        #endregion
    }
}
