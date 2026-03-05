using RimWorld;
using UnityEngine;
using Verse;

namespace USAC
{
    // 债券交易类
    public class Tradeable_Bond : Tradeable
    {
        #region 属性

        public override string Label => "USAC_Bond_Label".Translate();
        public override string TipDescription => "USAC_Bond_Desc".Translate();
        public override bool TraderWillTrade => true;

        #endregion

        #region 公共方法

        // 动态计算购买价
        public static float GetBondBuyPrice()
        {
            var faction = Find.FactionManager.FirstFactionOfDef(USAC_FactionDefOf.USAC_Faction);
            if (faction == null) return 2000f;

            // 关系映射价格
            float goodwill = faction.GoodwillWith(Faction.OfPlayer);
            float normalizedGoodwill = Mathf.Clamp01((goodwill + 100f) / 200f);
            float price = Mathf.Lerp(2000f, 1000f, normalizedGoodwill);

            return Mathf.Round(price);
        }

        public override float GetPriceFor(TradeAction action)
        {
            if (action == TradeAction.PlayerBuys)
            {
                return GetBondBuyPrice();
            }
            return 1000f;
        }

        #endregion
    }
}
