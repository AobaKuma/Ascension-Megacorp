using Verse;

namespace USAC
{
    // 贷款评估结果
    public struct UnifiedLoanEval
    {
        public float MaxAmount;
        public float InterestRate;
        public float GrowthRate;
        public DebtGrowthMode GrowthMode;
        public float Wealth;
        public float CreditDiscount;
        public bool IsAvailable;
        public string BlockReason;
    }

    // 财务交易记录
    public class USACDebtTransaction : IExposable
    {
        #region 字段
        public USACTransactionType Type;
        public float Amount;
        public string Note;
        public int TicksGame;
        #endregion

        #region 持久化
        public void ExposeData()
        {
            Scribe_Values.Look(ref Type, "Type");
            Scribe_Values.Look(ref Amount, "Amount");
            Scribe_Values.Look(ref Note, "Note");
            Scribe_Values.Look(ref TicksGame, "TicksGame");
        }
        #endregion
    }
}
