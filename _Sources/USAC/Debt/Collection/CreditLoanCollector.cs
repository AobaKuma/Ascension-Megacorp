using RimWorld;
using UnityEngine;
using Verse;

namespace USAC
{
    // 扣除信用分并降低好感
    public class CreditLoanCollector : ICollectionStrategy
    {
        public float Execute(Map map, float targetAmount,
            DebtContract contract)
        {
            var comp = GameComponent_USACDebt.Instance;
            if (comp == null) return 0f;

            // 大幅扣减信用分
            comp.CreditScore = Mathf.Max(0, comp.CreditScore - 25);

            // 降低USAC好感度
            var faction = Find.FactionManager.FirstFactionOfDef(
                USAC_FactionDefOf.USAC_Faction);
            if (faction != null)
            {
                faction.TryAffectGoodwillWith(
                    Faction.OfPlayer, -30, false, true);
            }

            // 信用分过低则封锁贸易
            if (comp.CreditScore <= 10)
            {
                Messages.Message(
                    "USAC.Debt.Message.CreditDanger".Translate(),
                    MessageTypeDefOf.ThreatBig);
            }
            else
            {
                Messages.Message(
                    "USAC.Debt.Message.CreditPenalty"
                        .Translate(comp.CreditScore),
                    MessageTypeDefOf.NegativeEvent);
            }

            // 信用贷无实物征收 返回0
            return 0f;
        }
    }
}
