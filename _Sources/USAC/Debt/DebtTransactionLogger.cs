using System.Collections.Generic;
using Verse;

namespace USAC
{
    // 债务交易记录器
    public class DebtTransactionLogger : IExposable
    {
        #region 字段
        public List<USACDebtTransaction> Transactions = new();
        private const int MaxTransactions = 50;
        #endregion

        #region 交易记录
        public void AddTransaction(USACTransactionType type, float amount, string note)
        {
            Transactions.Insert(0, new USACDebtTransaction
            {
                Type = type,
                Amount = amount,
                Note = note,
                TicksGame = Find.TickManager.TicksGame
            });

            if (Transactions.Count > MaxTransactions)
                Transactions.RemoveAt(Transactions.Count - 1);
        }
        #endregion

        #region 存档
        public void ExposeData()
        {
            Scribe_Collections.Look(ref Transactions, "Transactions", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.LoadingVars && Transactions == null)
                Transactions = new List<USACDebtTransaction>();
        }
        #endregion
    }
}
