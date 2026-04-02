using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace USAC
{
    // 贷款管理组件
    public class GameComponent_USACDebt : GameComponent
    {
        #region 字段
        public int CreditScore = 50;
        public List<DebtContract> ActiveContracts = new();
        public List<USACDebtTransaction> Transactions = new();

        // 债务系统被死锁标志
        public bool IsSystemLocked;

        // 当前收缴轮次是否有夹具被摧毁
        public bool HasGripperDestroyedThisRound;

        // 已摧毁据点计数
        public int DestroyedDebtSiteCount;

        // 结局触发标记
        public bool EndingTriggered;

        // 已清算殖民者列表
        public List<Pawn> LiquidatedPawns = new();

        // 锁定期还款次数
        public int RepayCountDuringLock;

        // 下次据点生成倒计时
        public int TicksUntilNextSiteBatch = -1;

        // 合同调度器
        private DebtScheduler scheduler = new DebtScheduler();

        // 旧版兼容迁移字段
        private float legacyTotalDebt;
        private float legacyInterest;
        #endregion

        public GameComponent_USACDebt(Game game) { }

        #region 属性
        // 总负债
        public float TotalDebt
        {
            get
            {
                float sum = 0f;
                for (int i = 0; i < ActiveContracts.Count; i++)
                {
                    var c = ActiveContracts[i];
                    if (c.IsActive) sum += c.Principal + c.AccruedInterest;
                }
                return sum;
            }
        }

        // 最近到期的合同
        public DebtContract NextDueContract
        {
            get
            {
                DebtContract best = null;
                for (int i = 0; i < ActiveContracts.Count; i++)
                {
                    var c = ActiveContracts[i];
                    if (c.IsActive && (best == null || c.NextCycleTick < best.NextCycleTick))
                        best = c;
                }
                return best;
            }
        }

        // 活跃合同数量
        public int ActiveCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < ActiveContracts.Count; i++)
                    if (ActiveContracts[i].IsActive) count++;
                return count;
            }
        }

        // 增加债务本金（服务费/租赁费）
        public void AddDebt(float amount, string reason)
        {
            if (ActiveContracts.Count == 0)
            {
                ApplyForLoan(DebtType.DynamicLoan, amount, 0.05f, 0.02f);
                return;
            }
            // 通过Handler统一处理
            DebtHandler.AdjustPrincipal(ActiveContracts[0], amount, reason, USACTransactionType.Initial);
        }

        public static GameComponent_USACDebt Instance =>
            Current.Game?.GetComponent<GameComponent_USACDebt>();

        // 返回财富最高的玩家殖民地地图
        public static Map GetRichestPlayerHomeMap()
        {
            Map best = null;
            float bestWealth = -1f;
            foreach (var map in Find.Maps)
            {
                if (!map.IsPlayerHome) continue;
                float w = map.wealthWatcher?.WealthTotal ?? 0f;
                if (w > bestWealth)
                {
                    bestWealth = w;
                    best = map;
                }
            }
            return best ?? Find.AnyPlayerHomeMap;
        }
        #endregion

        #region 生命周期
        public override void StartedNewGame()
        {
            if (ActiveContracts == null)
                ActiveContracts = new List<DebtContract>();
            if (LiquidatedPawns == null)
                LiquidatedPawns = new List<Pawn>();
        }

        public override void LoadedGame()
        {
            if (ActiveContracts == null)
                ActiveContracts = new List<DebtContract>();
            if (LiquidatedPawns == null)
                LiquidatedPawns = new List<Pawn>();

            MigrateLegacyData();

            // 重建调度器回调
            scheduler.RebuildCallbacks(this);

            // 尝试修复旧版卡死存档的孤儿合同
            RepairOrphanedSchedules();
        }

        public override void GameComponentTick()
        {
            if (ActiveContracts == null || ActiveContracts.Count == 0)
                return;

            int now = Find.TickManager.TicksGame;

            // 调度器检查并触发到期事件
            scheduler.CheckAndTrigger(now);

            // 清理已结清的合同
            for (int i = ActiveContracts.Count - 1; i >= 0; i--)
            {
                var contract = ActiveContracts[i];
                if (contract.IsActive && contract.Principal <= 0)
                {
                    contract.IsActive = false;
                    scheduler.UnscheduleContract(contract.ContractId);
                }
            }

            // 据点生成倒计时
            if (IsSystemLocked && TicksUntilNextSiteBatch > 0)
            {
                TicksUntilNextSiteBatch--;
                if (TicksUntilNextSiteBatch <= 0)
                {
                    GenerateSiteBatch();
                    TicksUntilNextSiteBatch = 900000;
                }
            }
        }

        private void GenerateSiteBatch()
        {
            // 检查是否有升级合同
            bool hasEscalated = false;
            for (int i = 0; i < ActiveContracts.Count; i++)
            {
                if (ActiveContracts[i].IsActive && ActiveContracts[i].ConsecutiveCollectionFails >= 2)
                {
                    hasEscalated = true;
                    break;
                }
            }

            if (!hasEscalated) return;

            int count = Rand.RangeInclusive(1, 2);
            var contract = NextDueContract;
            if (contract == null) return;

            Map map = GetRichestPlayerHomeMap();
            for (int i = 0; i < count; i++)
            {
                TryGenerateDebtSite(contract, map);
            }
        }




        // 检查剧本债务是否全部清偿
        public void CheckDebtSettledEnding()
        {
            if (IsSystemLocked) return;
            if (ActiveContracts == null || ActiveContracts.Count == 0) return;

            // 必须存在过剧本合同且全部结清
            bool allSettled = true;
            for (int i = 0; i < ActiveContracts.Count; i++)
            {
                if (ActiveContracts[i].IsActive) { allSettled = false; break; }
            }
            if (allSettled)
                USAC.Endings.USACEndingManager.TriggerDebtSettled();
        }

        #endregion

        #region 周期结算
        internal void ProcessContractCycle(DebtContract contract)
        {
            // 合同已结清则立即退出
            if (!contract.IsActive || contract.Principal <= 0)
            {
                contract.IsActive = false;
                scheduler.UnscheduleContract(contract.ContractId);
                return;
            }

            Map map = GetRichestPlayerHomeMap();

            // 确定基准时间 防止漂移
            int baseTick = Math.Max(contract.NextCycleTick, Find.TickManager.TicksGame);

            // 连续抗缴2次跳过结算
            if (contract.ConsecutiveCollectionFails >= 2)
            {
                contract.ProcessCycle(map);
                contract.NextCycleTick = baseTick + DebtContract.CycleTicks;

                // 重新调度下次周期
                scheduler.ScheduleContractCycle(contract, () => ProcessContractCycle(contract));
                return;
            }

            // 执行周期结算
            contract.ProcessCycle(map);

            // 弹窗询问还款
            ShowRepaymentDialog(contract, map);

            // 重置周期
            contract.NextCycleTick = baseTick + DebtContract.CycleTicks;

            // 重新调度下次周期
            scheduler.ScheduleContractCycle(contract, () => ProcessContractCycle(contract));
        }

        private void ShowRepaymentDialog(
            DebtContract contract, Map map)
        {
            float toPay = contract.AccruedInterest;
            int bondsNeeded = Mathf.CeilToInt(toPay / 1000f);

            string text =
                "USAC.Debt.Dialog.Repayment.Text".Translate(
                    contract.Label,
                    toPay.ToString("N0"),
                    bondsNeeded,
                    contract.Principal.ToString("N0"),
                    contract.MissedPayments);
            if (contract.MissedPayments >= 2) text += "USAC.Debt.Dialog.Repayment.Warning".Translate();

            DiaNode diaNode = new DiaNode(text);

            // 确认缴纳
            DiaOption optPay = new DiaOption(
                "USAC.Debt.Dialog.Repayment.Option.Pay".Translate())
            {
                action = () =>
                {
                    if (contract.TryPayInterest(map))
                    {
                        Messages.Message(
                            "USAC.Debt.Message.InterestPaid".Translate(contract.Label),
                            MessageTypeDefOf.PositiveEvent);
                    }
                    else
                    {
                        HandleFailedPayment(contract, map);
                    }
                },
                resolveTree = true
            };

            // 拒绝/无力偿还
            DiaOption optRefuse = new DiaOption(
                "USAC.Debt.Dialog.Repayment.Option.Refuse".Translate())
            {
                action = () =>
                {
                    HandleFailedPayment(contract, map);
                },
                resolveTree = true
            };

            diaNode.options.Add(optPay);
            diaNode.options.Add(optRefuse);

            Find.WindowStack.Add(new Dialog_NodeTree(
                diaNode, true, false,
                "USAC.Debt.Dialog.Repayment.Title".Translate(contract.Label)));
        }

        private void HandleFailedPayment(
            DebtContract contract, Map map)
        {
            CreditScore = Mathf.Max(0, CreditScore - 15);
            contract.HandleMissedPayment();

            // 达到失败上限停止轨道收缴
            // 转为定期生成据点
            if (contract.ConsecutiveCollectionFails >= 2)
            {
                // 由Tick计时器统一处理
                return;
            }

            // 失败次数不到2次时轨道收缴
            if (contract.ShouldForceCollect)
            {
                ForceCollect(contract, map);
            }
        }
        #endregion

        #region 强制征收
        public void HandleCollectionFailure(DebtContract contract, Map map)
        {
            contract.ConsecutiveCollectionFails++;

            // 第1轮抗缴警告锁定
            if (contract.ConsecutiveCollectionFails == 1)
            {
                IsSystemLocked = true;
                CreditScore = 0;
                Find.LetterStack.ReceiveLetter(
                    "USAC_DebtSite_WarningLetterLabel".Translate(),
                    "USAC_DebtSite_WarningLetterText".Translate(contract.Label),
                    LetterDefOf.NegativeEvent);
            }
            // 抗缴升级据点模式并弹窗
            else if (contract.ConsecutiveCollectionFails == 2)
            {
                Find.LetterStack.ReceiveLetter(
                    "USAC_DebtSite_EscalationLetterLabel".Translate(),
                    "USAC_DebtSite_EscalationLetterText".Translate(contract.Label),
                    LetterDefOf.ThreatBig);
            }
            // 后续抗缴转为生成据点
        }

        public void ForceCollect(DebtContract contract, Map map)
        {
            if (map == null) return;

            // 重置本轮收缴状态
            HasGripperDestroyedThisRound = false;

            // 检测地图封闭状态
            if (IsMapSealedFromOrbit(map))
            {
                Messages.Message(
                    "USAC.Debt.Message.ForceCollectPausedByShield".Translate(),
                    MessageTypeDefOf.NeutralEvent);

                HandleCollectionFailure(contract, map);
                return;
            }

            var strategy = CollectionStrategyFactory.Create(contract.Type);
            float targetAmount = contract.AccruedInterest > 0
                ? contract.AccruedInterest
                : contract.Principal * 0.1f;

            // 这里只是派遣夹具（异步过程）
            strategy.Execute(map, targetAmount, contract);

            AddTransaction(USACTransactionType.Penalty,
                0, // 实际金额在夹子离开时记录
                "USAC.Debt.Transaction.ForceCollect".Translate(
                    contract.Label,
                    contract.MissedPayments));
        }

        // 收缴轮次结果评审
        public void CheckCollectionRoundResults(Map map)
        {
            if (map == null) return;

            // 检查剩余夹子数量
            int activeGrippers = 0;
            var grippers = map.listerThings.ThingsOfDef(USAC_DefOf.USAC_GripperIncoming);
            for (int i = 0; i < grippers.Count; i++)
            {
                // 当前夹子在Destroy中
                if (!grippers[i].Destroyed) activeGrippers++;
            }

            // 等待最后一个夹子结束
            if (activeGrippers > 1) return;

            // 违约升级判定
            if (HasGripperDestroyedThisRound)
            {
                Log.Message("[USAC] 判定本轮收缴存在抗缴行为。");
                var contract = NextDueContract;
                if (contract != null)
                {
                    HandleCollectionFailure(contract, map);
                }
                HasGripperDestroyedThisRound = false;
            }

            // 殖民地清算结局判定
            CheckDebtLiquidationEndingInternal(map);
        }

        private void CheckDebtLiquidationEndingInternal(Map map)
        {
            if (EndingTriggered) return;
            if (ActiveCount == 0) return;

            // 检查人员存活
            var maps = Find.Maps;
            for (int i = 0; i < maps.Count; i++)
            {
                if (maps[i].mapPawns.FreeColonistsSpawnedOrInPlayerEjectablePodsCount > 0) return;
            }
            var caravans = Find.WorldObjects.Caravans;
            for (int i = 0; i < caravans.Count; i++)
            {
                if (caravans[i].IsPlayerControlled && caravans[i].pawns != null)
                {
                    if (caravans[i].PawnsListForReading.Any(p => p.IsFreeColonist)) return;
                }
            }
            if (QuestUtility.TotalBorrowedColonistCount() > 0) return;

            // 触发清算结局
            USAC.Endings.USACEndingManager.TriggerDebtLiquidation();
        }

        public void TryGenerateDebtSite(DebtContract contract, Map map)
        {
            RimWorld.Planet.PlanetTile siteTile;
            RimWorld.Planet.PlanetTile nearTile = Find.RandomPlayerHomeMap?.Tile ?? RimWorld.Planet.PlanetTile.Invalid;

            if (!RimWorld.Planet.TileFinder.TryFindNewSiteTile(out siteTile, nearTile, 4, 12, true, null, 0.5f, true, RimWorld.Planet.TileFinderMode.Random, false, false))
                return;

            var choices = new List<SitePartDef>();
            if (USAC_DefOf.USAC_CommercialOutpost != null) choices.Add(USAC_DefOf.USAC_CommercialOutpost);
            if (USAC_DefOf.USAC_ArtilleryPosition != null) choices.Add(USAC_DefOf.USAC_ArtilleryPosition);
            if (USAC_DefOf.USAC_Airbase != null) choices.Add(USAC_DefOf.USAC_Airbase);

            if (choices.Count == 0) return;
            var siteDef = choices.RandomElement();

            Log.Message($"[USAC] 为订单 {contract.Label} 随机选择了据点类型: {siteDef.defName}");

            var sitePartDefWithParams = new RimWorld.Planet.SitePartDefWithParams(siteDef, new RimWorld.Planet.SitePartParams());
            var parts = new List<RimWorld.Planet.SitePartDefWithParams> { sitePartDefWithParams };

            Faction faction = Find.FactionManager.FirstFactionOfDef(USAC_FactionDefOf.USAC_Faction) ?? Find.FactionManager.RandomEnemyFaction(false, false, true, TechLevel.Industrial);
            RimWorld.Planet.Site site = (RimWorld.Planet.Site)RimWorld.Planet.WorldObjectMaker.MakeWorldObject(USAC_DefOf.USAC_DebtSite);
            site.Tile = siteTile;
            site.SetFaction(faction);
            foreach (var part in parts)
            {
                site.AddPart(new RimWorld.Planet.SitePart(site, part.def, part.parms));
            }

            Find.WorldObjects.Add(site);

            Find.LetterStack.ReceiveLetter("USAC_DebtSite_LetterLabel".Translate(), "USAC_DebtSite_LetterText".Translate(contract.Label), LetterDefOf.ThreatBig, site);
        }

        // 检查地图屏蔽状态
        private static bool IsMapSealedFromOrbit(Map map)
        {
            // 判定全屋顶覆盖
            var roofGrid = map.roofGrid;
            int total = map.cellIndices.NumGridCells;
            for (int i = 0; i < total; i++)
            {
                if (roofGrid.RoofAt(i) == null)
                    return false;
            }

            // 判定四周边界封闭
            int w = map.Size.x;
            int h = map.Size.z;
            for (int x = 0; x < w; x++)
            {
                if (new IntVec3(x, 0, 0).Walkable(map)) return false;
                if (new IntVec3(x, 0, h - 1).Walkable(map)) return false;
            }
            for (int z = 1; z < h - 1; z++)
            {
                if (new IntVec3(0, 0, z).Walkable(map)) return false;
                if (new IntVec3(w - 1, 0, z).Walkable(map)) return false;
            }

            return true;
        }
        #endregion

        #region 贷款申请
        public void ApplyForLoan(DebtType type, float amount,
            float growthRate, float interestRate,
            DebtGrowthMode growthMode = DebtGrowthMode.PrincipalBased)
        {
            float debtAmount = amount;

            var contract = new DebtContract(
                type, debtAmount, growthRate, interestRate, growthMode);

            ActiveContracts.Add(contract);
            CreditScore = Mathf.Max(0, CreditScore - 2);

            // 注册到调度器
            scheduler.ScheduleContractCycle(contract, () => ProcessContractCycle(contract));

            // 投放债券
            Map map = GetRichestPlayerHomeMap();
            if (map != null)
            {
                int bondCount = Mathf.FloorToInt(amount / 1000f);
                if (bondCount > 0)
                {
                    Thing bonds = ThingMaker.MakeThing(
                        USAC_DefOf.USAC_Bond);
                    bonds.stackCount = bondCount;
                    DropPodUtility.DropThingsNear(
                        DropCellFinder.TradeDropSpot(map),
                        map, new[] { bonds });
                }
            }

            AddTransaction(USACTransactionType.Initial,
                debtAmount,
                "USAC.Debt.Transaction.SignContract".Translate(contract.Label));
        }

        // 贷款风险定价评估
        public UnifiedLoanEval EvaluateLoan(float interestRate, float growthRate, DebtGrowthMode growthMode)
        {
            float wealth = GetRichestPlayerHomeMap()?.wealthWatcher?.WealthTotal ?? 0f;
            float totalDebt = TotalDebt;

            // 基础信用系数
            float baseCreditFactor = Mathf.Lerp(0.1f, 0.3f, (CreditScore - 30f) / 70f);
            if (CreditScore < 30) baseCreditFactor = 0f;

            // 利率参数加成
            float interestBonus = interestRate * 1.5f;

            // 风险参数加成
            float growthBonus = growthRate * 2.5f;

            // 结算综合倍率
            float totalMult = baseCreditFactor + interestBonus + growthBonus;

            // 环境信用折扣
            float creditDiscount = Mathf.Clamp01((CreditScore - 30) / 175f) * 0.30f;
            float actualInterest = Mathf.Round(interestRate * (1f - creditDiscount) * 1000f) / 1000f;

            // 计算可用额度
            float rawMax = wealth * totalMult - totalDebt;
            float maxAmount = Mathf.Floor(Mathf.Max(0f, rawMax) / 1000f) * 1000f;

            string blockReason = null;
            if (IsSystemLocked)
                blockReason = "USAC_DebtSite_LoanLockedWarning".Translate();
            else if (CreditScore < 30)
                blockReason = "USAC.UI.Assets.Block.LowCredit".Translate();
            else if (maxAmount < 1000f)
                blockReason = "USAC.UI.Assets.Block.LowWealth".Translate();

            return new UnifiedLoanEval
            {
                MaxAmount = maxAmount,
                InterestRate = actualInterest,
                GrowthRate = growthRate,
                GrowthMode = growthMode,
                Wealth = wealth,
                CreditDiscount = creditDiscount,
                IsAvailable = blockReason == null,
                BlockReason = blockReason
            };
        }

        // 返回距下次结算的可读时间字符串
        public static string GetTimeToNextCycle(DebtContract c)
        {
            int ticks = c.NextCycleTick - Find.TickManager.TicksGame;
            if (ticks <= 0) return "USAC.UI.Assets.Imminent".Translate();
            return GenDate.ToStringTicksToPeriod(ticks, false);
        }

        // 预测下一周期本金增长量
        public static float PredictNextGrowth(DebtContract c)
        {
            if (c.GrowthRate <= 0f) return 0f;
            if (c.GrowthMode == DebtGrowthMode.WealthBased)
                return (GetRichestPlayerHomeMap()?.wealthWatcher?.WealthTotal ?? 0f) * c.GrowthRate;
            return c.Principal * c.GrowthRate;
        }
        #endregion

        #region 债券操作(公开给合同使用)
        public int GetBondCountNearBeacons(Map map)
        {
            if (map == null) return 0;
            int count = 0;
            var buildings = map.listerBuildings.allBuildingsColonist;
            for (int i = 0; i < buildings.Count; i++)
            {
                if (buildings[i] is not Building_OrbitalTradeBeacon beacon) continue;
                foreach (IntVec3 c in beacon.TradeableCells)
                {
                    var bond = c.GetFirstThing(map, USAC_DefOf.USAC_Bond);
                    if (bond != null) count += bond.stackCount;
                }
            }
            return count;
        }

        public void ConsumeBondsNearBeacons(Map map, int count)
        {
            int remaining = count;
            var buildings = map.listerBuildings.allBuildingsColonist;
            for (int i = 0; i < buildings.Count; i++)
            {
                if (buildings[i] is not Building_OrbitalTradeBeacon beacon) continue;
                foreach (IntVec3 c in beacon.TradeableCells)
                {
                    var bond = c.GetFirstThing(map, USAC_DefOf.USAC_Bond);
                    if (bond == null) continue;
                    int take = Math.Min(remaining, bond.stackCount);
                    bond.SplitOff(take).Destroy();
                    remaining -= take;
                    if (remaining <= 0) return;
                }
            }
        }

        public int GetBondCountOnMap()
        {
            Map map = Find.AnyPlayerHomeMap;
            if (map == null) return 0;
            var bonds = map.listerThings.ThingsOfDef(USAC_DefOf.USAC_Bond);
            int count = 0;
            for (int i = 0; i < bonds.Count; i++)
                count += bonds[i].stackCount;
            return count;
        }

        public void ConsumeBonds(Map map, int count)
        {
            int remaining = count;
            foreach (var b in map.listerThings
                .ThingsOfDef(USAC_DefOf.USAC_Bond))
            {
                int take = Math.Min(remaining, b.stackCount);
                b.SplitOff(take).Destroy();
                remaining -= take;
                if (remaining <= 0) break;
            }
        }
        #endregion

        #region 交易记录
        public void AddTransaction(USACTransactionType type,
            float amount, string note)
        {
            Transactions.Insert(0, new USACDebtTransaction
            {
                Type = type,
                Amount = amount,
                Note = note,
                TicksGame = Find.TickManager.TicksGame
            });
            if (Transactions.Count > 50)
                Transactions.RemoveAt(Transactions.Count - 1);
        }
        #endregion

        #region 调试接口
        // 修复旧版卡死导致的孤儿合同调度
        private void RepairOrphanedSchedules()
        {
            int now = Find.TickManager.TicksGame;
            for (int i = 0; i < ActiveContracts.Count; i++)
            {
                var c = ActiveContracts[i];
                if (!c.IsActive) continue;

                // NextCycleTick损坏修复
                if (c.NextCycleTick <= 0)
                {
                    c.NextCycleTick = now + 1;
                    Log.Warning($"[USAC] 合同 {c.Label}({c.ContractId}) NextCycleTick损坏 已修复为立即触发");
                }

                // 补注缺失的调度事件
                if (!scheduler.IsContractScheduled(c.ContractId))
                {
                    var contract = c;
                    scheduler.ScheduleContractCycle(contract, () => ProcessContractCycle(contract));
                    Log.Warning($"[USAC] 合同 {c.Label}({c.ContractId}) 调度事件缺失 已自动补注");
                }
            }
        }

        // 开发者接口
        public void Debug_SkipCycle()
        {
            if (ActiveContracts == null) return;
            for (int i = ActiveContracts.Count - 1; i >= 0; i--)
            {
                var contract = ActiveContracts[i];
                if (contract.IsActive)
                {
                    ProcessContractCycle(contract);
                }
            }
        }

        // 刷新系统锁定状态
        public void RefreshSystemLockStatus()
        {
            if (!IsSystemLocked) return;

            bool anyViolation = false;
            for (int i = 0; i < ActiveContracts.Count; i++)
            {
                var c = ActiveContracts[i];
                if (!c.IsActive) continue;
                if (c.ConsecutiveCollectionFails > 0 || c.HasActiveDebtSite)
                {
                    anyViolation = true;
                    break;
                }
            }

            if (!anyViolation)
            {
                IsSystemLocked = false;
                Log.Message("[USAC] 连续守信表现已核实，系统锁定已解除。");
            }
        }
        #endregion

        #region 存档
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref CreditScore, "CreditScore", 50);
            Scribe_Values.Look(ref IsSystemLocked, "IsSystemLocked", false);
            Scribe_Values.Look(ref RepayCountDuringLock, "RepayCountDuringLock", 0);
            Scribe_Values.Look(ref EndingTriggered, "EndingTriggered", false);
            Scribe_Values.Look(ref TicksUntilNextSiteBatch, "TicksUntilNextSiteBatch", -1);

            // 旧版兼容读取
            Scribe_Values.Look(ref legacyTotalDebt, "TotalDebt");
            Scribe_Values.Look(ref legacyInterest,
                "TotalInterestAccrued");

            Scribe_Collections.Look(ref ActiveContracts,
                "ActiveContracts", LookMode.Deep);
            Scribe_Collections.Look(ref Transactions,
                "Transactions", LookMode.Deep);
            Scribe_Collections.Look(ref LiquidatedPawns,
                "LiquidatedPawns", LookMode.Reference);

            // 调度器存档
            Scribe_Deep.Look(ref scheduler, "scheduler");
            if (scheduler == null)
                scheduler = new DebtScheduler();
        }

        // 旧存档迁移
        private void MigrateLegacyData()
        {
            if (legacyTotalDebt <= 0) return;

            bool hasActive = false;
            for (int i = 0; i < ActiveContracts.Count; i++)
            {
                if (ActiveContracts[i].IsActive) { hasActive = true; break; }
            }
            if (hasActive) return;

            var legacy = new DebtContract(
                DebtType.WholeMortgage,
                legacyTotalDebt, 0.20f, 0.05f,
                DebtGrowthMode.WealthBased);
            legacy.AccruedInterest = legacyInterest;

            ActiveContracts.Add(legacy);

            // 注册到调度器
            scheduler.ScheduleContractCycle(legacy, () => ProcessContractCycle(legacy));

            legacyTotalDebt = 0;
            legacyInterest = 0;

            Log.Message("USAC.Debt.Log.LegacyMigrated".Translate());
        }
        #endregion
    }
}
