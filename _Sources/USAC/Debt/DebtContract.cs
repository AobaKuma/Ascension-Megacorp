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
                DebtType.WholeMortgage => "整体抵押贷款",
                DebtType.DynamicLoan => "动态信贷",
                _ => "未知合同"
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
                Principal += growth;
                var comp = GameComponent_USACDebt.Instance;
                comp?.AddTransaction(USACTransactionType.GrowthAdjust,
                    growth, $"{Label} 本金增长({GrowthRate * 100:F0}%)");
            }

            // 结算利息
            float rawInterest = Principal * InterestRate;
            AccruedInterest = Mathf.Max(1000f, CeilTo1000(rawInterest));

            var debtComp = GameComponent_USACDebt.Instance;
            debtComp?.AddTransaction(USACTransactionType.Interest,
                AccruedInterest, $"{Label} 周期利息");

            string msg = $"[USAC] {Label}: 本金增长+₿{growth:F0}" +
                         $", 本期利息₿{AccruedInterest:F0}";
            Messages.Message(msg, MessageTypeDefOf.NegativeEvent);
        }

        // 处理欠缴罚则
        public void HandleMissedPayment()
        {
            MissedPayments++;
            // 利息转入本金
            Principal += AccruedInterest;
            AccruedInterest = 0f;

            var comp = GameComponent_USACDebt.Instance;
            comp?.AddTransaction(USACTransactionType.Penalty,
                0, $"{Label} 欠缴第{MissedPayments}次 利息并入本金");
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
            AccruedInterest = 0f;
            comp.CreditScore = Mathf.Min(100, comp.CreditScore + 5);
            comp.AddTransaction(USACTransactionType.Payment,
                paid, $"{Label} 利息缴纳");
            return true;
        }

        // 偿还本金逻辑
        public string TryPayPrincipal(Map map, int bondCount)
        {
            if (AccruedInterest > 0)
                return "请先缴纳当期利息";

            CheckQuarterReset();

            float payAmount = bondCount * 1000f;
            float freeLimit = Principal * 0.10f;
            float remaining = freeLimit - PrincipalPaidThisQuarter;
            if (remaining < 0) remaining = 0;

            // 计算手续费
            float totalThisQuarter = PrincipalPaidThisQuarter + payAmount;
            float surcharge = SurchargeTable.Calculate(
                Principal, totalThisQuarter);
            float prevSurcharge = SurchargeTable.Calculate(
                Principal, PrincipalPaidThisQuarter);
            float incrementalFee = surcharge - prevSurcharge;

            // 计算总成本
            int feeBonds = Mathf.CeilToInt(incrementalFee / 1000f);
            int totalBonds = bondCount + feeBonds;

            var comp = GameComponent_USACDebt.Instance;
            int bondsAvail = comp?.GetBondCountOnMap() ?? 0;

            if (bondsAvail < totalBonds)
                return $"需要{totalBonds}张债券(含手续费{feeBonds}张)";

            comp.ConsumeBonds(map, totalBonds);
            Principal = Mathf.Max(0, Principal - payAmount);
            PrincipalPaidThisQuarter += payAmount;
            comp.CreditScore = Mathf.Min(100, comp.CreditScore + 2);

            comp.AddTransaction(USACTransactionType.Payment,
                payAmount, $"{Label} 本金偿还");
            if (incrementalFee > 0)
            {
                comp.AddTransaction(USACTransactionType.Surcharge,
                    feeBonds * 1000f, $"{Label} 超额还款手续费");
            }

            // 检查结清状态
            if (Principal <= 0)
            {
                IsActive = false;
                Messages.Message($"[USAC] {Label} 已结清！",
                    MessageTypeDefOf.PositiveEvent);
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
        }
        #endregion
    }
}
