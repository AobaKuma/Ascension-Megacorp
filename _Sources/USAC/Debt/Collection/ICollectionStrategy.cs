using Verse;

namespace USAC
{
    // 强制征收策略接口
    public interface ICollectionStrategy
    {
        // 执行征收并返回实际征收价值
        float Execute(Map map, float targetAmount,
            DebtContract contract);
    }
}
