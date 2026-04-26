using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace USAC
{
    // 债务管理游戏组件（重构版）
    // 职责：协调子组件、维护全局状态、处理结局逻辑
    public class GameComponent_USACDebt : GameComponent
    {
        #region 子组件
        public DebtContractManager ContractManager = new();
        public DebtCollectionCoordinator CollectionCoordinator = new();
        public DebtSiteGenerator SiteGenerator = new();
        public DebtTransactionLogger TransactionLogger = new();
        #endregion

        #region 全局状态字段
        public int CreditScore = 50;
        public bool IsSystemLocked;
        public int DestroyedDebtSiteCount;
        public bool EndingTriggered;
        public List<Pawn> LiquidatedPawns = new();
        public int RepayCountDuringLock;

        // 下次据点批量生成的绝对游戏Tick
        private int nextSiteBatchTick = -1;

        // 旧版兼容迁移字段
        private float legacyTotalDebt;
        private float legacyInterest;
        #endregion

        public GameComponent_USACDebt(Game game) { }

        #region 属性
        // 供Alert读取的剩余tick数
        public int TicksUntilNextSiteBatch
        {
            get
            {
                if (nextSiteBatchTick < 0) return -1;
                return Math.Max(0, nextSiteBatchTick - Find.TickManager.TicksGame);
            }
            set
            {
                if (value < 0)
                    nextSiteBatchTick = -1;
                else
                    nextSiteBatchTick = Find.TickManager.TicksGame + value;
            }
        }

        // 委托到 ContractManager
        public float TotalDebt => ContractManager.TotalDebt;
        public DebtContract NextDueContract => ContractManager.NextDueContract;
        public int ActiveCount => ContractManager.ActiveCount;
        public List<DebtContract> ActiveContracts => ContractManager.ActiveContracts;

        // 全局据点模式判断
        public bool IsInGlobalSiteMode
        {
            get
            {
                if (!IsSystemLocked) return false;
                for (int i = 0; i < ActiveContracts.Count; i++)
                {
                    if (ActiveContracts[i].IsActive && ActiveContracts[i].IsInSiteMode)
                        return true;
                }
                return false;
            }
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
            if (LiquidatedPawns == null)
                LiquidatedPawns = new List<Pawn>();
        }

        public override void LoadedGame()
        {
            if (LiquidatedPawns == null)
                LiquidatedPawns = new List<Pawn>();

            MigrateLegacyData();
            ContractManager.LoadedGame(this);

            // 订阅债务事件
            SubscribeToDebtEvents();
        }

        // 订阅债务事件
        private void SubscribeToDebtEvents()
        {
            // 订阅合同结清事件
            DebtEventBus.Instance.Subscribe(DebtEventType.ContractSettled, OnContractSettled);

            // 订阅本金变更事件
            DebtEventBus.Instance.Subscribe(DebtEventType.PrincipalChanged, OnPrincipalChanged);

            // 订阅利息累积事件
            DebtEventBus.Instance.Subscribe(DebtEventType.InterestAccrued, OnInterestAccrued);
        }

        // 处理合同结清事件
        private void OnContractSettled(DebtEventArgs args)
        {
            CheckDebtSettledEnding();
            RefreshSystemLockStatus();
        }

        // 处理本金变更事件
        private void OnPrincipalChanged(DebtEventArgs args)
        {
            RefreshSystemLockStatus();
        }

        // 处理利息累积事件
        private void OnInterestAccrued(DebtEventArgs args)
        {
            // 可以在此添加利息累积的额外逻辑
        }

        public override void GameComponentTick()
        {
            // 合同管理器Tick
            ContractManager.Tick();

            int now = Find.TickManager.TicksGame;

            // 据点批量生成计时检查
            if (IsSystemLocked && nextSiteBatchTick > 0 && now >= nextSiteBatchTick)
            {
                GenerateSiteBatch();
                nextSiteBatchTick = nextSiteBatchTick + 900000;
            }
        }

        private void GenerateSiteBatch()
        {
            // 检查是否有升级合同
            bool hasEscalated = false;
            for (int i = 0; i < ActiveContracts.Count; i++)
            {
                if (ActiveContracts[i].IsActive && ActiveContracts[i].IsInSiteMode)
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
            SiteGenerator.GenerateSiteBatch(contract, map, count);
        }

        // 检查剧本债务是否全部清偿
        public void CheckDebtSettledEnding()
        {
            if (IsSystemLocked) return;
            if (ActiveContracts == null || ActiveContracts.Count == 0) return;

            bool allSettled = true;
            for (int i = 0; i < ActiveContracts.Count; i++)
            {
                if (ActiveContracts[i].IsActive) { allSettled = false; break; }
            }
            if (allSettled)
                USAC.Endings.USACEndingManager.TriggerDebtSettled();
        }
        #endregion

        #region 对外接口（保持兼容性）
        // 增加债务本金
        public void AddDebt(float amount, string reason)
        {
            ContractManager.AddDebt(amount, reason);
            TransactionLogger.AddTransaction(USACTransactionType.Initial, amount, reason);
        }

        // 申请贷款
        public void ApplyForLoan(DebtType type, float amount,
            float growthRate, float interestRate,
            DebtGrowthMode growthMode = DebtGrowthMode.PrincipalBased)
        {
            ContractManager.ApplyForLoan(type, amount, growthRate, interestRate, growthMode);
            CreditScore = Mathf.Max(0, CreditScore - 2);
            TransactionLogger.AddTransaction(USACTransactionType.Initial, amount,
                "USAC.Debt.Transaction.SignContract".Translate($"{type}_{Find.TickManager.TicksGame}"));
        }

        // 处理失败还款
        public void HandleFailedPayment(DebtContract contract, Map map)
        {
            CreditScore = Mathf.Max(0, CreditScore - 15);
            contract.HandleMissedPayment();

            // 达到失败上限停止轨道收缴
            if (contract.ConsecutiveCollectionFails >= 2)
            {
                return;
            }

            // 失败次数不到2次时轨道收缴
            if (contract.ShouldForceCollect)
            {
                CollectionCoordinator.ForceCollect(contract, map);
                TransactionLogger.AddTransaction(USACTransactionType.Penalty, 0,
                    "USAC.Debt.Transaction.ForceCollect".Translate(contract.Label, contract.MissedPayments));
            }
        }

        // 收缴轮次结果评审
        public void CheckCollectionRoundResults(Map map)
        {
            CollectionCoordinator.CheckCollectionRoundResults(map);
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

        // 贷款风险定价评估
        public UnifiedLoanEval EvaluateLoan(float interestRate, float growthRate, DebtGrowthMode growthMode)
        {
            return DebtEvaluator.EvaluateLoan(CreditScore, TotalDebt, IsSystemLocked,
                interestRate, growthRate, growthMode);
        }

        // 债券操作（委托到静态工具类）
        public int GetBondCountNearBeacons(Map map) => DebtBondOperations.GetBondCountNearBeacons(map);
        public void ConsumeBondsNearBeacons(Map map, int count) => DebtBondOperations.ConsumeBondsNearBeacons(map, count);
        public int GetBondCountOnMap() => DebtBondOperations.GetBondCountOnMap(Find.AnyPlayerHomeMap);
        public void ConsumeBonds(Map map, int count) => DebtBondOperations.ConsumeBonds(map, count);

        // 交易记录
        public void AddTransaction(USACTransactionType type, float amount, string note)
        {
            TransactionLogger.AddTransaction(type, amount, note);
        }

        public List<USACDebtTransaction> Transactions => TransactionLogger.Transactions;

        // 静态工具方法委托
        public static string GetTimeToNextCycle(DebtContract c) => DebtEvaluator.GetTimeToNextCycle(c);
        public static float PredictNextGrowth(DebtContract c) => DebtEvaluator.PredictNextGrowth(c);

        // 暴露给外部的属性
        public bool HasGripperDestroyedThisRound
        {
            get => CollectionCoordinator.HasGripperDestroyedThisRound;
            set => CollectionCoordinator.HasGripperDestroyedThisRound = value;
        }

        // 暴露给 DebtScheduler 的方法
        public void ProcessContractCycle(DebtContract contract)
        {
            ContractManager.ProcessContractCycle(contract);
        }
        #endregion

        #region 调试接口
        public void Debug_SkipCycle()
        {
            if (ActiveContracts == null) return;
            for (int i = ActiveContracts.Count - 1; i >= 0; i--)
            {
                var contract = ActiveContracts[i];
                if (contract.IsActive)
                {
                    ContractManager.ProcessContractCycle(contract);
                }
            }
        }

        public void RefreshSystemLockStatus()
        {
            if (!IsSystemLocked) return;

            bool anyViolation = false;
            for (int i = 0; i < ActiveContracts.Count; i++)
            {
                var c = ActiveContracts[i];
                if (!c.IsActive) continue;
                if (c.ConsecutiveCollectionFails > 0 || c.HasActiveDebtSite || c.IsInSiteMode)
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
            Scribe_Values.Look(ref nextSiteBatchTick, "TicksUntilNextSiteBatch", -1);
            Scribe_Values.Look(ref DestroyedDebtSiteCount, "DestroyedDebtSiteCount", 0);

            // 旧版兼容读取
            Scribe_Values.Look(ref legacyTotalDebt, "TotalDebt");
            Scribe_Values.Look(ref legacyInterest, "TotalInterestAccrued");

            Scribe_Collections.Look(ref LiquidatedPawns, "LiquidatedPawns", LookMode.Reference);

            // 子组件存档
            Scribe_Deep.Look(ref ContractManager, "ContractManager");
            Scribe_Deep.Look(ref CollectionCoordinator, "CollectionCoordinator");
            Scribe_Deep.Look(ref SiteGenerator, "SiteGenerator");
            Scribe_Deep.Look(ref TransactionLogger, "TransactionLogger");

            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                if (ContractManager == null) ContractManager = new DebtContractManager();
                if (CollectionCoordinator == null) CollectionCoordinator = new DebtCollectionCoordinator();
                if (SiteGenerator == null) SiteGenerator = new DebtSiteGenerator();
                if (TransactionLogger == null) TransactionLogger = new DebtTransactionLogger();
                if (LiquidatedPawns == null) LiquidatedPawns = new List<Pawn>();
            }
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

            ContractManager.ActiveContracts.Add(legacy);

            legacyTotalDebt = 0;
            legacyInterest = 0;

            Log.Message("USAC.Debt.Log.LegacyMigrated".Translate());
        }
        #endregion
    }
}
