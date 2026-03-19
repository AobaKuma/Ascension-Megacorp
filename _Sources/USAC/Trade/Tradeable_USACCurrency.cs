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
                if (placeholder == null || placeholder.Destroyed)
                {
                    placeholder = ThingMaker.MakeThing(USAC_DefOf.USAC_Bond);
                }
                return placeholder;
            }
        }

        public static bool EnableBondsForPayment { get; set; } = true;
        public static bool UseBondsForPayment { get; set; } = true;

        #endregion

        #region 属性

        public override bool IsCurrency => true;
        public override bool TraderWillTrade => true;
        public override string Label => BuildCurrencyLabel();
        public override string TipDescription => BuildCurrencyDescription();
        public override float BaseMarketValue => 1f;
        public override TransferablePositiveCountDirection PositiveCountDirection => TransferablePositiveCountDirection.Source;

        // 获取代表性物品实例
        public override Thing AnyThing
        {
            get
            {
                // 优先返回债券作为代表物品
                if (thingsColony != null && thingsColony.Count > 0)
                {
                    for (int i = 0; i < thingsColony.Count; i++)
                    {
                        if (thingsColony[i] != null && thingsColony[i].def == USAC_DefOf.USAC_Bond)
                            return thingsColony[i];
                    }
                    // 没有债券则返回第一个非空物品
                    for (int i = 0; i < thingsColony.Count; i++)
                    {
                        if (thingsColony[i] != null)
                            return thingsColony[i];
                    }
                }
                
                if (thingsTrader != null && thingsTrader.Count > 0)
                {
                    for (int i = 0; i < thingsTrader.Count; i++)
                    {
                        if (thingsTrader[i] != null && thingsTrader[i].def == USAC_DefOf.USAC_Bond)
                            return thingsTrader[i];
                    }
                    for (int i = 0; i < thingsTrader.Count; i++)
                    {
                        if (thingsTrader[i] != null)
                            return thingsTrader[i];
                    }
                }
                
                // 确保永远返回非空值
                return Placeholder;
            }
        }

        // 获取对应的物品定义
        public override ThingDef ThingDef => USAC_DefOf.USAC_Bond;

        #endregion

        #region 公共方法

        public override int CostToInt(float cost) => Mathf.CeilToInt(cost);

        public override int GetMinimumToTransfer()
        {
            if (PositiveCountDirection == TransferablePositiveCountDirection.Destination)
            {
                return -CountHeldBy(Transactor.Trader);
            }
            return -CountHeldBy(Transactor.Colony);
        }

        public override int GetMaximumToTransfer()
        {
            if (PositiveCountDirection == TransferablePositiveCountDirection.Destination)
            {
                return CountHeldBy(Transactor.Colony);
            }
            return CountHeldBy(Transactor.Trader);
        }

        public override int CountHeldBy(Transactor trans)
        {
            List<Thing> things = (trans == Transactor.Colony) ? thingsColony : thingsTrader;
            if (things == null || things.Count == 0)
                return 0;
            
            float corpseBagValue = CalculateCorpseBagValue(things);
            float bondValue = EnableBondsForPayment ? (CalculateBondCount(things) * 1000f) : 0f;
            return Mathf.RoundToInt(corpseBagValue + bondValue);
        }

        public override void ResolveTrade()
        {
            if (ActionToDo == TradeAction.PlayerSells)
            {
                int transferAmount = CountToTransferToDestination;
                if (transferAmount > 0)
                {
                    TransferPlayerCurrency(transferAmount);
                }
            }
        }

        public override int GetHashCode() => "USACCurrency".GetHashCode();

        #endregion

        #region 私有逻辑

        private string BuildCurrencyLabel()
        {
            int corpseBagCount = CountCorpseBags(thingsColony);
            int bondCount = EnableBondsForPayment ? CalculateBondCount(thingsColony) : 0;
            
            string label;
            if (corpseBagCount > 0 && bondCount > 0)
                label = "USAC_Currency_LabelFull".Translate(corpseBagCount, bondCount);
            else if (bondCount > 0)
                label = "USAC_Currency_LabelBondOnly".Translate(bondCount);
            else if (corpseBagCount > 0)
                label = "USAC_Currency_LabelCorpseBagOnly".Translate(corpseBagCount);
            else
                label = "USAC_Currency_LabelEmpty".Translate();

            // 原版UI追加总价值及距下袋可用
            if (!Find.WindowStack.IsOpen<Dialog_USACTerminal>())
            {
                float totalValue = CalculateTotalValue(thingsColony);
                float smallest = GetSmallestBagValue(thingsColony);
                
                if (totalValue > 0)
                {
                    label += $" [₿{Mathf.RoundToInt(totalValue)}]";
                    if (smallest > 0)
                    {
                        label += $" (" + "USAC_Currency_UntilNextBag".Translate(Mathf.RoundToInt(smallest)) + ")";
                    }
                }
            }

            return label;
        }

        private string BuildCurrencyDescription()
        {
            var sb = new StringBuilder();
            sb.AppendLine("USAC_Currency_Desc".Translate());
            sb.AppendLine();

            float corpseBagValue = CalculateCorpseBagValue(thingsColony);
            float totalValue = corpseBagValue;
            float smallestBagValue = GetSmallestBagValue(thingsColony);
            
            sb.AppendLine("USAC_Currency_Breakdown".Translate());
            sb.AppendLine("USAC_Currency_CorpseBagValue".Translate(Mathf.RoundToInt(corpseBagValue)));
            
            if (EnableBondsForPayment)
            {
                int bondCount = CalculateBondCount(thingsColony);
                float bondValue = bondCount * 1000f;
                totalValue += bondValue;
                sb.AppendLine("USAC_Currency_BondValue".Translate(bondCount, Mathf.RoundToInt(bondValue)));
            }
            
            sb.AppendLine();
            sb.AppendLine("USAC_Currency_TotalValue".Translate(Mathf.RoundToInt(totalValue)));
            sb.AppendLine();
            if (smallestBagValue > 0)
            {
                sb.AppendLine("USAC_Currency_SmallestBag".Translate(Mathf.RoundToInt(smallestBagValue)));
            }

            return sb.ToString();
        }

        private float CalculateTotalValue(List<Thing> things)
        {
            return CalculateCorpseBagValue(things) + (CalculateBondCount(things) * 1000f);
        }

        private float CalculateCorpseBagValue(List<Thing> things)
        {
            if (things == null || things.Count == 0)
                return 0f;
            
            float total = 0f;
            for (int i = 0; i < things.Count; i++)
            {
                if (things[i] == null)
                    continue;
                
                if (things[i] is Building_USACCorpseStorage storage)
                {
                    foreach (Thing t in storage.GetDirectlyHeldThings())
                    {
                        if (t is Corpse c) total += Building_CorpseBag.CalculateCorpseValue(c);
                    }
                }
                else if (things[i] is Building_CorpseBag bag && bag.HasCorpse)
                {
                    total += Building_CorpseBag.CalculateCorpseValue(bag.ContainedCorpse);
                }
            }
            return total;
        }

        private float GetSmallestBagValue(List<Thing> things)
        {
            if (things == null || things.Count == 0)
                return 0f;
            
            float smallest = float.MaxValue;
            bool found = false;

            for (int i = 0; i < things.Count; i++)
            {
                if (things[i] == null)
                    continue;
                
                if (things[i] is Building_USACCorpseStorage storage)
                {
                    foreach (Thing t in storage.GetDirectlyHeldThings())
                    {
                        if (t is Corpse c)
                        {
                            float cv = Building_CorpseBag.CalculateCorpseValue(c);
                            if (cv > 0 && cv < smallest)
                            {
                                smallest = cv;
                                found = true;
                            }
                        }
                    }
                }
                else if (things[i] is Building_CorpseBag bag && bag.HasCorpse)
                {
                    float value = Building_CorpseBag.CalculateCorpseValue(bag.ContainedCorpse);
                    if (value > 0 && value < smallest)
                    {
                        smallest = value;
                        found = true;
                    }
                }
            }
            return found ? smallest : 0f;
        }

        private int CountCorpseBags(List<Thing> things)
        {
            if (things == null || things.Count == 0)
                return 0;
            
            int count = 0;
            for (int i = 0; i < things.Count; i++)
            {
                if (things[i] == null)
                    continue;
                
                if (things[i] is Building_USACCorpseStorage storage)
                {
                    foreach (Thing t in storage.GetDirectlyHeldThings())
                    {
                        if (t is Corpse) count++;
                    }
                }
                else if (things[i] is Building_CorpseBag bag && bag.HasCorpse)
                {
                    count++;
                }
            }
            return count;
        }

        private int CalculateBondCount(List<Thing> things)
        {
            if (things == null || things.Count == 0)
                return 0;
            
            int count = 0;
            for (int i = 0; i < things.Count; i++)
            {
                if (things[i] != null && things[i].def == USAC_DefOf.USAC_Bond)
                    count += things[i].stackCount;
            }
            return count;
        }

        private void TransferPlayerCurrency(float valueToTransfer)
        {
            if (EnableBondsForPayment && UseBondsForPayment)
            {
                float remaining = TransferBonds(valueToTransfer);
                if (remaining > 0) TransferCorpseBags(remaining);
            }
            else
            {
                float remaining = TransferCorpseBags(valueToTransfer);
                if (EnableBondsForPayment && remaining > 0) TransferBonds(remaining);
            }
        }

        private float TransferCorpseBags(float remaining)
        {
            if (thingsColony == null || thingsColony.Count == 0)
                return remaining;
            
            var items = new List<CorpseTradeItem>();
            for (int i = 0; i < thingsColony.Count; i++)
            {
                if (thingsColony[i] == null) continue;
                
                if (thingsColony[i] is Building_USACCorpseStorage storage)
                {
                    var heldThings = storage.GetDirectlyHeldThings();
                    if (heldThings != null)
                    {
                        foreach (Thing t in heldThings)
                        {
                            if (t is Corpse c)
                            {
                                items.Add(new CorpseTradeItem { corpse = c, parent = storage, isStorage = true });
                            }
                        }
                    }
                }
                else if (thingsColony[i] is Building_CorpseBag bag && bag.HasCorpse)
                {
                    items.Add(new CorpseTradeItem { corpse = bag.ContainedCorpse, parent = bag, isStorage = false });
                }
            }

            items.SortBy(x => Building_CorpseBag.CalculateCorpseValue(x.corpse));

            foreach (var item in items)
            {
                if (remaining <= 0) break;

                float val = Building_CorpseBag.CalculateCorpseValue(item.corpse);
                
                if (item.isStorage)
                {
                    // 仅销毁尸体不移动建筑
                    Building_USACCorpseStorage storage = (Building_USACCorpseStorage)item.parent;
                    item.corpse.Destroy();
                    storage.InvalidateMarketValueCache();
                }
                else
                {
                    // 移动实体建筑物并放置蓝图
                    Building_CorpseBag bag = (Building_CorpseBag)item.parent;
                    Map map = bag.Map;
                    IntVec3 pos = bag.Position;
                    Rot4 rot = bag.Rotation;
                    ThingDef bagDef = bag.def;
                    ThingDef stuff = bag.Stuff;
                    Faction faction = bag.Faction;

                    if (bag.Spawned) bag.DeSpawn();
                    TradeSession.trader.GiveSoldThingToTrader(bag, 1, TradeSession.playerNegotiator);

                    if (map != null && faction == Faction.OfPlayer)
                    {
                        GenConstruct.PlaceBlueprintForBuild(bagDef, pos, map, rot, faction, stuff);
                    }
                }

                remaining -= val;
            }
            return remaining;
        }

        private struct CorpseTradeItem
        {
            public Corpse corpse;
            public Building parent;
            public bool isStorage;
        }

        private float TransferBonds(float remaining)
        {
            if (thingsColony == null || thingsColony.Count == 0)
                return remaining;
            
            for (int i = 0; i < thingsColony.Count; i++)
            {
                if (remaining <= 0) break;

                var bond = thingsColony[i];
                if (bond == null || bond.def != USAC_DefOf.USAC_Bond) continue;
                
                int bondsNeeded = Mathf.CeilToInt(remaining / 1000f);
                int toTransfer = Mathf.Min(bondsNeeded, bond.stackCount);
                if (toTransfer > 0)
                {
                    Thing split = bond.SplitOff(toTransfer);
                    if (split != null)
                    {
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
