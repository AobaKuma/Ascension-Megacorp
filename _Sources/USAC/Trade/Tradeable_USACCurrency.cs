using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace USAC
{
    // USAC 货币整合类
    public class Tradeable_USACCurrency : Tradeable
    {
        #region 字段

        // 占位防止第三方访问空
        private static Thing placeholder;

        private static Thing Placeholder
        {
            get
            {
                if (placeholder == null) placeholder = ThingMaker.MakeThing(USAC_DefOf.USAC_Bond);
                return placeholder;
            }
        }

        #endregion

        #region 属性

        public override bool IsCurrency => true;
        public override bool TraderWillTrade => true;
        public override string Label => BuildCurrencyLabel();
        public override string TipDescription => BuildCurrencyDescription();
        public override float BaseMarketValue => 1f;

        // 获取代表性物品实例
        public override Thing AnyThing
        {
            get
            {
                if (thingsColony.Count > 0) return thingsColony[0];
                if (thingsTrader.Count > 0) return thingsTrader[0];
                return Placeholder;
            }
        }

        // 获取对应的物品定义
        public override ThingDef ThingDef => USAC_DefOf.USAC_Bond;

        #endregion

        #region 公共方法

        public override int CostToInt(float cost) => Mathf.CeilToInt(cost);

        public override int CountHeldBy(Transactor trans)
        {
            List<Thing> things = (trans == Transactor.Colony) ? thingsColony : thingsTrader;
            return Mathf.RoundToInt(CalculateTotalValue(things));
        }

        public override void ResolveTrade()
        {
            if (ActionToDo == TradeAction.PlayerSells)
            {
                TransferPlayerCurrency(CountToTransferToDestination);
            }
        }

        public override int GetHashCode() => "USACCurrency".GetHashCode();

        #endregion

        #region 私有逻辑

        private string BuildCurrencyLabel()
        {
            float smallestBagValue = GetSmallestBagValue(thingsColony);
            int bondCount = CalculateBondCount(thingsColony);
            float total = smallestBagValue + (bondCount * 1000f);

            if (smallestBagValue > 0 && bondCount > 0)
                return "USAC_Currency_LabelFull".Translate(Mathf.RoundToInt(total), bondCount);
            if (bondCount > 0)
                return "USAC_Currency_LabelBondOnly".Translate(bondCount);
            if (smallestBagValue > 0)
                return "USAC_Currency_LabelCorpseBagOnly".Translate(Mathf.RoundToInt(smallestBagValue));

            return "USAC_Currency_LabelEmpty".Translate();
        }

        private string BuildCurrencyDescription()
        {
            var sb = new StringBuilder();
            sb.AppendLine("USAC_Currency_Desc".Translate());
            sb.AppendLine();

            var corpseBagValue = CalculateCorpseBagValue(thingsColony);
            var bondCount = CalculateBondCount(thingsColony);
            var bondValue = bondCount * 1000f;
            float smallestBagValue = GetSmallestBagValue(thingsColony);

            sb.AppendLine("USAC_Currency_Breakdown".Translate());
            sb.AppendLine("USAC_Currency_CorpseBagValue".Translate(Mathf.RoundToInt(corpseBagValue)));
            sb.AppendLine("USAC_Currency_BondValue".Translate(bondCount, Mathf.RoundToInt(bondValue)));
            sb.AppendLine();
            sb.AppendLine("USAC_Currency_TotalValue".Translate(Mathf.RoundToInt(corpseBagValue + bondValue)));
            sb.AppendLine();
            if (smallestBagValue > 0)
            {
                sb.AppendLine("USAC_Currency_SmallestBag".Translate(Mathf.RoundToInt(smallestBagValue)));
                sb.AppendLine("USAC_Currency_UntilNextBag".Translate(Mathf.RoundToInt(smallestBagValue + bondValue)));
            }

            return sb.ToString();
        }

        private float CalculateTotalValue(List<Thing> things)
        {
            return CalculateCorpseBagValue(things) + (CalculateBondCount(things) * 1000f);
        }

        private float CalculateCorpseBagValue(List<Thing> things)
        {
            float total = 0f;
            for (int i = 0; i < things.Count; i++)
            {
                if (things[i] is Building_CorpseBag bag && bag.HasCorpse)
                    total += Building_CorpseBag.CalculateCorpseValue(bag.ContainedCorpse);
            }
            return total;
        }

        private float GetSmallestBagValue(List<Thing> things)
        {
            float smallest = float.MaxValue;
            bool found = false;

            for (int i = 0; i < things.Count; i++)
            {
                if (things[i] is Building_CorpseBag bag && bag.HasCorpse)
                {
                    float value = Building_CorpseBag.CalculateCorpseValue(bag.ContainedCorpse);
                    if (value < smallest)
                    {
                        smallest = value;
                        found = true;
                    }
                }
            }
            return found ? smallest : 0f;
        }

        private int CalculateBondCount(List<Thing> things)
        {
            int count = 0;
            for (int i = 0; i < things.Count; i++)
            {
                if (things[i].def == USAC_DefOf.USAC_Bond)
                    count += things[i].stackCount;
            }
            return count;
        }

        private void TransferPlayerCurrency(float valueToTransfer)
        {
            float remaining = TransferBonds(valueToTransfer);
            if (remaining > 0) TransferCorpseBags(remaining);
        }

        private float TransferCorpseBags(float remaining)
        {
            List<Building_CorpseBag> filledBags = new();
            for (int i = 0; i < thingsColony.Count; i++)
            {
                if (thingsColony[i] is Building_CorpseBag bag && bag.HasCorpse)
                    filledBags.Add(bag);
            }

            filledBags.SortBy(b => Building_CorpseBag.CalculateCorpseValue(b.ContainedCorpse));

            for (int i = 0; i < filledBags.Count; i++)
            {
                if (remaining <= 0) break;

                var bag = filledBags[i];
                float bagValue = Building_CorpseBag.CalculateCorpseValue(bag.ContainedCorpse);
                if (bag.Spawned) bag.DeSpawn();

                TradeSession.trader.GiveSoldThingToTrader(bag, 1, TradeSession.playerNegotiator);
                remaining -= bagValue;
            }
            return remaining;
        }

        private float TransferBonds(float remaining)
        {
            for (int i = 0; i < thingsColony.Count; i++)
            {
                if (remaining <= 0) break;

                var bond = thingsColony[i];
                if (bond.def == USAC_DefOf.USAC_Bond)
                {
                    int bondsNeeded = Mathf.CeilToInt(remaining / 1000f);
                    int toTransfer = Mathf.Min(bondsNeeded, bond.stackCount);
                    if (toTransfer > 0)
                    {
                        Thing split = bond.SplitOff(toTransfer);
                        TradeSession.trader.GiveSoldThingToTrader(split, toTransfer, TradeSession.playerNegotiator);
                        remaining -= toTransfer * 1000f;
                    }
                }
            }
            return remaining;
        }

        #endregion
    }
}
