using System;
using RimWorld;
using Verse;
using UnityEngine;

namespace USAC
{
    // 债务核心处理器
    public static class DebtHandler
    {
        #region 核心修改接口
        // 修改本金
        public static void AdjustPrincipal(DebtContract contract, float amount, string reason, USACTransactionType type = USACTransactionType.Interest)
        {
            if (contract == null) return;

            float oldPrincipal = contract.Principal;
            contract.Principal = Mathf.Max(0, contract.Principal + amount);

            // 记录交易
            GameComponent_USACDebt.Instance?.AddTransaction(type, amount, reason);

            // 发布本金变更事件
            DebtEventBus.Instance.Publish(new DebtEventArgs
            {
                EventType = DebtEventType.PrincipalChanged,
                Contract = contract,
                Amount = amount,
                Reason = reason,
                Data = new { OldPrincipal = oldPrincipal, NewPrincipal = contract.Principal }
            });

            // 联动反应
            if (amount < 0) // 还款行为
            {
                var comp = GameComponent_USACDebt.Instance;
                if (comp != null && comp.IsSystemLocked)
                {
                    comp.RepayCountDuringLock++;
                    Log.Message($"[USAC] 死锁期间还款计数: {comp.RepayCountDuringLock}/2");
                }
                OnDebtReduced(contract, Math.Abs(amount));
            }
        }

        // 修改利息
        public static void SetAccruedInterest(DebtContract contract, float amount)
        {
            if (contract == null) return;
            contract.AccruedInterest = amount;

            // 发布利息累积事件
            DebtEventBus.Instance.Publish(new DebtEventArgs
            {
                EventType = DebtEventType.InterestAccrued,
                Contract = contract,
                Amount = amount
            });
        }
        #endregion

        #region 系统反馈逻辑
        // 债务减少时的副作用
        private static void OnDebtReduced(DebtContract contract, float reducedAmount)
        {
            var comp = GameComponent_USACDebt.Instance;
            if (comp == null) return;

            // 信用恢复逻辑
            int creditBonus = Mathf.FloorToInt(reducedAmount / 5000f);
            if (creditBonus > 0)
            {
                comp.CreditScore = Mathf.Min(100, comp.CreditScore + creditBonus);
            }

            // 合同还清检查
            if (contract.Principal <= 0)
            {
                contract.IsActive = false;
                Messages.Message("USAC.Debt.Message.ContractSettled".Translate(contract.Label), MessageTypeDefOf.PositiveEvent);

                // 发布合同结清事件
                DebtEventBus.Instance.Publish(new DebtEventArgs
                {
                    EventType = DebtEventType.ContractSettled,
                    Contract = contract,
                    Amount = 0,
                    Reason = "Contract fully paid"
                });

                // 解除系统锁定
                if (contract.ConsecutiveCollectionFails > 0)
                {
                    contract.ConsecutiveCollectionFails = 0;
                    comp.RefreshSystemLockStatus();
                }
            }
        }
        #endregion
    }
}
