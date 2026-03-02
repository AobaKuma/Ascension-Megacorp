namespace USAC
{
    // 征收策略工厂
    public static class CollectionStrategyFactory
    {
        public static ICollectionStrategy Create(DebtType type)
        {
            return type switch
            {
                DebtType.WholeMortgage => new WholeMortgageCollector(),
                _ => new WholeMortgageCollector()
            };
        }
    }
}
