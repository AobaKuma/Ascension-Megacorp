using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace USAC
{
    // 贷款合同类
    public class DebtContract : IExposable
    {
        #region 标识符
        public string ContractId;
        public DebtType Type;
        public string Label;
        #endregion

        #region 财务数据
        public float Principal;
        public float AccruedInterest;
        #endregion

        #region 参数配置
        public DebtGrowthMode GrowthMode;
        public float GrowthRate;
        public float InterestRate;
        #endregion

        #region 周期状态
        public int NextCycleTick = -1;
        public int MissedPayments;
        // 季度还款追踪
        public float PrincipalPaidThisQuarter;
        public int QuarterStartTick;
        public bool IsActive = true;

        // 强制征收连续失败与据点管控
        public int ConsecutiveCollectionFails;
        public bool HasActiveDebtSite;

        // 已升级为据点收缴模式（不可逆）
        public bool IsInSiteMode;
        #endregion

        #region 时间常量
        // 双季度结算周期
        public const int CycleTicks = GenDate.TicksPerQuadrum * 2;
        // 单季度重置周期
        public const int QuarterTicks = GenDate.TicksPerQuadrum;
        #endregion

        #region 构造方法
        public DebtContract() { }

        public DebtContract(DebtType type, float principal,
            float growthRate, float interestRate,
            DebtGrowthMode growthMode = DebtGrowthMode.WealthBased)
        {
            ContractId = $"{type}_{Find.TickManager.TicksGame}";
            Type = type;
            Label = GetDefaultLabel(type);
            Principal = principal;
            GrowthMode = growthMode;
            GrowthRate = growthRate;
            InterestRate = interestRate;

            int now = Find.TickManager.TicksGame;
            NextCycleTick = now + CycleTicks;
            QuarterStartTick = now;
        }
        #endregion

        #region 逻辑处理
        // 获取默认标签
        private static string GetDefaultLabel(DebtType type)
        {
            return type switch
            {
                DebtType.WholeMortgage =>
                    "USAC.Debt.Type.WholeMortgage".Translate(),
                DebtType.DynamicLoan =>
                    "USAC.Debt.Type.DynamicLoan".Translate(),
                _ => "USAC.Debt.Type.Unknown".Translate()
            };
        }

        // 处理结算周期
        public void ProcessCycle(Map map)
        {
            if (!IsActive || Principal <= 0) return;

            // 本金增长
            float growth = CalculateGrowth(map);
            if (growth > 0)
            {
                DebtHandler.AdjustPrincipal(this, growth, 
                    "USAC.Debt.Transaction.PrincipalGrowth".Translate(Label, (GrowthRate * 100f).ToString("F0")),
                    USACTransactionType.GrowthAdjust);
            }

            // 结算利息
            float rawInterest = Principal * InterestRate;
            float interestAmount = Mathf.Max(1000f, CeilTo1000(rawInterest));
            DebtHandler.SetAccruedInterest(this, interestAmount);

            GameComponent_USACDebt.Instance?.AddTransaction(USACTransactionType.Interest,
                interestAmount,
                "USAC.Debt.Transaction.CycleInterest".Translate(Label));

            Messages.Message("USAC.Debt.Message.CycleProcessed".Translate(Label, growth.ToString("F0"), interestAmount.ToString("F0")), 
                MessageTypeDefOf.NegativeEvent);
        }

        // 处理欠缴罚则
        public void HandleMissedPayment()
        {
            MissedPayments++;
            // 利息转入本金
            float penalty = AccruedInterest;
            DebtHandler.AdjustPrincipal(this, penalty, 
                "USAC.Debt.Transaction.MissedPayment".Translate(Label, MissedPayments),
                USACTransactionType.Penalty);
            
            DebtHandler.SetAccruedInterest(this, 0f);
        }

        // 检查强制征收
        public bool ShouldForceCollect => MissedPayments >= 3;

        // 支付利息逻辑
        public bool TryPayInterest(Map map)
        {
            if (AccruedInterest <= 0) return true;

            int bondsNeeded = Mathf.CeilToInt(AccruedInterest / 1000f);
            var comp = GameComponent_USACDebt.Instance;
            int bondsAvail = comp?.GetBondCountNearBeacons(map) ?? 0;

            if (bondsAvail < bondsNeeded) return false;

            comp.ConsumeBondsNearBeacons(map, bondsNeeded);
            float paid = bondsNeeded * 1000f;
            
            // 记录日志并重置利息
            comp.AddTransaction(USACTransactionType.Payment, paid, "USAC.Debt.Transaction.InterestPayment".Translate(Label));
            DebtHandler.SetAccruedInterest(this, 0f);

            // 成功还款降低违规热度
            if (ConsecutiveCollectionFails > 0) ConsecutiveCollectionFails--;
            comp.CreditScore = Mathf.Min(100, comp.CreditScore + 5);

            DebtHandler.NotifyFinancialStateChanged();
            return true;
        }

        // 偿还本金逻辑
        public string TryPayPrincipal(Map map, int bondCount)
        {
            if (AccruedInterest > 0)
                return "USAC.Debt.Error.PayInterestFirst".Translate();

            CheckQuarterReset();

            float payAmount = bondCount * 1000f;
            
            // 计算手续费
            float totalThisQuarter = PrincipalPaidThisQuarter + payAmount;
            float surcharge = SurchargeTable.Calculate(Principal, totalThisQuarter);
            float prevSurcharge = SurchargeTable.Calculate(Principal, PrincipalPaidThisQuarter);
            float incrementalFee = surcharge - prevSurcharge;

            int feeBonds = Mathf.CeilToInt(incrementalFee / 1000f);
            int totalBonds = bondCount + feeBonds;

            var comp = GameComponent_USACDebt.Instance;
            int bondsAvail = comp?.GetBondCountOnMap() ?? 0;

            if (bondsAvail < totalBonds)
                return "USAC.Debt.Error.NotEnoughBondsWithFee".Translate(totalBonds, feeBonds);

            comp.ConsumeBonds(map, totalBonds);
            
            // 通过Handler执行
            DebtHandler.AdjustPrincipal(this, -payAmount, 
                "USAC.Debt.Transaction.PrincipalRepay".Translate(Label), 
                USACTransactionType.Payment);

            PrincipalPaidThisQuarter += payAmount;

            if (incrementalFee > 0)
            {
                comp.AddTransaction(USACTransactionType.Surcharge, feeBonds * 1000f, 
                    "USAC.Debt.Transaction.RepaySurcharge".Translate(Label));
            }

            return null;
        }

        // 检查季度重置
        private void CheckQuarterReset()
        {
            int now = Find.TickManager.TicksGame;
            if (now - QuarterStartTick >= QuarterTicks)
            {
                PrincipalPaidThisQuarter = 0f;
                QuarterStartTick = now;
            }
        }

        // 计算本金增长
        private float CalculateGrowth(Map map)
        {
            if (GrowthMode == DebtGrowthMode.WealthBased)
            {
                float wealth = map?.wealthWatcher?.WealthTotal ?? 0f;
                return wealth * GrowthRate;
            }
            return Principal * GrowthRate;
        }

        // 数额取整逻辑
        public static float CeilTo1000(float value)
        {
            if (value <= 0) return 0;
            return Mathf.CeilToInt(value / 1000f) * 1000f;
        }
        #endregion

        #region 数据持久化
        public void ExposeData()
        {
            Scribe_Values.Look(ref ContractId, "ContractId");
            Scribe_Values.Look(ref Type, "Type");
            Scribe_Values.Look(ref Label, "Label");
            Scribe_Values.Look(ref Principal, "Principal");
            Scribe_Values.Look(ref AccruedInterest, "AccruedInterest");
            Scribe_Values.Look(ref GrowthMode, "GrowthMode");
            Scribe_Values.Look(ref GrowthRate, "GrowthRate");
            Scribe_Values.Look(ref InterestRate, "InterestRate");
            Scribe_Values.Look(ref NextCycleTick, "NextCycleTick", -1);
            Scribe_Values.Look(ref MissedPayments, "MissedPayments");
            Scribe_Values.Look(ref PrincipalPaidThisQuarter,
                "PrincipalPaidThisQuarter");
            Scribe_Values.Look(ref QuarterStartTick, "QuarterStartTick");
            Scribe_Values.Look(ref IsActive, "IsActive", true);
            
            Scribe_Values.Look(ref ConsecutiveCollectionFails, "ConsecutiveCollectionFails");
            Scribe_Values.Look(ref HasActiveDebtSite, "HasActiveDebtSite");
            Scribe_Values.Look(ref IsInSiteMode, "IsInSiteMode");
        }
        #endregion
    }
}
