namespace USAC
{
    #region 核心枚举
    // 贷款类型
    public enum DebtType
    {
        WholeMortgage,    // 整体抵押
        DynamicLoan       // 动态信贷
    }

    // 增长模式
    public enum DebtGrowthMode
    {
        WealthBased,    // 财富基准
        PrincipalBased  // 本金基准
    }

    // 利率档位
    public enum LoanRatePreset
    {
        Conservative,  // 保守档
        Standard,      // 标准档
        Aggressive     // 激进档
    }

    // 交易记录类型
    public enum USACTransactionType
    {
        Initial,     // 初始借贷
        Interest,    // 利息结算
        Payment,     // 主动还款
        Penalty,     // 强制征收
        Surcharge,   // 手续费
        GrowthAdjust // 本金增长
    }

    // 征收优先级
    public enum CollectionPriority
    {
        Items = 0,
        Buildings = 1,
        Prisoners = 2,
        Mechs = 3,
        Colonists = 4
    }
    #endregion

    #region 手续费算法
    public static class SurchargeTable
    {
        // 阶梯费率表
        private static readonly (float threshold, float rate)[] Tiers =
        {
            (0.10f, 0.0f),
            (0.20f, 0.5f),
            (0.30f, 1.0f),
            (0.50f, 2.0f),
            (float.MaxValue, 3.0f)
        };

        // 计算超额手续费
        public static float Calculate(float principal, float totalPaid)
        {
            if (principal <= 0) return 0f;

            float fee = 0f;
            float ratio = totalPaid / principal;

            float prevThreshold = 0f;
            foreach (var (threshold, rate) in Tiers)
            {
                if (ratio <= prevThreshold)
                    break;

                float bandStart = prevThreshold;
                float bandEnd = System.Math.Min(ratio, threshold);
                float bandWidth = bandEnd - bandStart;

                if (bandWidth > 0 && bandStart >= 0.10f)
                {
                    fee += bandWidth * principal * rate;
                }

                prevThreshold = threshold;
            }

            return fee;
        }
    }
    #endregion
}
