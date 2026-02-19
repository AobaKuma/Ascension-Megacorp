using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace USAC
{
    // 标记项目专用货币
    public class ModExtension_CorpseBagTrader : DefModExtension
    {
        public bool useCorpseBagCurrency = true;
        public string corpseBagDefName = "USAC_CorpseBag";
    }

    [HarmonyPatch(typeof(TradeDeal), "AddAllTradeables")]
    public static class Patch_TradeDeal_AddAllTradeables
    {
        public static void Postfix(List<Tradeable> ___tradeables)
        {
            if (!TradeSession.Active)
                return;

            var trader = TradeSession.trader;
            if (trader?.TraderKind == null)
                return;

            var ext = trader.TraderKind.GetModExtension<ModExtension_CorpseBagTrader>();
            if (ext == null || !ext.useCorpseBagCurrency)
                return;

            AddUSACCurrency(___tradeables, ext);
            AddBondTradeable(___tradeables);
        }

        // 添加USAC货币
        private static void AddUSACCurrency(List<Tradeable> tradeables, ModExtension_CorpseBagTrader ext)
        {
            // 移除白银货币
            tradeables.RemoveAll(t => t.ThingDef == ThingDefOf.Silver && t.IsCurrency);

            ThingDef corpseBagDef = DefDatabase<ThingDef>.GetNamedSilentFail(ext.corpseBagDefName);
            ThingDef bondDef = DefDatabase<ThingDef>.GetNamedSilentFail("USAC_Bond");

            // 移除原交易项缓存

            var currency = new Tradeable_USACCurrency();
            Map map = TradeSession.playerNegotiator.Map;

            if (map == null)
            {
                tradeables.Add(currency);
                return;
            }

            HashSet<Thing> addedThings = new();

            // 扫描交易信标范围内的物品
            foreach (var beacon in Building_OrbitalTradeBeacon.AllPowered(map))
            {
                foreach (var cell in beacon.TradeableCells)
                {
                    List<Thing> thingList = cell.GetThingList(map);
                    foreach (var thing in thingList)
                    {
                        if (addedThings.Contains(thing))
                            continue;

                        // 尸体袋
                        if (corpseBagDef != null && thing is Building_CorpseBag bag &&
                            bag.def == corpseBagDef && bag.HasCorpse &&
                            bag.Faction == Faction.OfPlayer && !bag.IsForbidden(Faction.OfPlayer))
                        {
                            currency.AddThing(bag, Transactor.Colony);
                            addedThings.Add(thing);
                        }
                        // 债券
                        else if (bondDef != null && thing.def == bondDef &&
                            thing.def.category == ThingCategory.Item &&
                            !thing.IsForbidden(Faction.OfPlayer))
                        {
                            currency.AddThing(thing, Transactor.Colony);
                            addedThings.Add(thing);
                        }
                    }
                }
            }

            tradeables.Add(currency);
        }

        // 添加债券买入Tradeable
        private static void AddBondTradeable(List<Tradeable> tradeables)
        {
            ThingDef bondDef = DefDatabase<ThingDef>.GetNamedSilentFail("USAC_Bond");
            if (bondDef == null)
                return;

            var bondTradeable = new Tradeable_Bond();

            // 添加商人的债券
            foreach (var thing in TradeSession.trader.Goods)
            {
                if (thing.def == bondDef)
                    bondTradeable.AddThing(thing, Transactor.Trader);
            }

            // 只有商人有债券时才添加
            if (bondTradeable.thingsTrader.Count > 0)
            {
                tradeables.Add(bondTradeable);
            }
        }
    }

    [HarmonyPatch(typeof(TradeDeal), "get_CurrencyTradeable")]
    public static class Patch_TradeDeal_CurrencyTradeable
    {
        public static bool Prefix(List<Tradeable> ___tradeables, ref Tradeable __result)
        {
            if (!TradeSession.Active)
                return true;

            var trader = TradeSession.trader;
            if (trader?.TraderKind == null)
                return true;

            var ext = trader.TraderKind.GetModExtension<ModExtension_CorpseBagTrader>();
            if (ext == null || !ext.useCorpseBagCurrency)
                return true;

            // 优先查找USAC货币
            foreach (var tradeable in ___tradeables)
            {
                if (tradeable is Tradeable_USACCurrency)
                {
                    __result = tradeable;
                    return false;
                }
            }

            // 兼容尸体袋交易
            foreach (var tradeable in ___tradeables)
            {
                if (tradeable is Tradeable_CorpseBag)
                {
                    __result = tradeable;
                    return false;
                }
            }

            __result = null;
            return false;
        }
    }

    // 触发成交后机兵空投
    [HarmonyPatch(typeof(Tradeable), "ResolveTrade")]
    public static class Patch_Tradeable_ResolveTrade
    {
        public static bool Prefix(Tradeable __instance)
        {
            USAC_Debug.Log($"[USAC] ResolveTrade Prefix: ThingDef={__instance.ThingDef?.defName}, ActionToDo={__instance.ActionToDo}");

            if (__instance.ActionToDo != TradeAction.PlayerBuys)
            {
                USAC_Debug.Log("[USAC] Not PlayerBuys, skipping");
                return true;
            }

            var mechOrderExt = __instance.ThingDef?.GetModExtension<ModExtension_MechOrder>();
            USAC_Debug.Log($"[USAC] mechOrderExt={mechOrderExt}, mechKindDef={mechOrderExt?.mechKindDef?.defName}");

            if (mechOrderExt?.mechKindDef == null)
            {
                USAC_Debug.Log("[USAC] No ModExtension_MechOrder, skipping");
                return true;
            }

            int countBought = __instance.CountToTransferToSource;
            USAC_Debug.Log($"[USAC] countBought={countBought}");

            if (countBought <= 0)
            {
                USAC_Debug.Log("[USAC] countBought <= 0, skipping");
                return true;
            }

            USAC_Debug.Log($"[USAC] Dropping {countBought} mechs: {mechOrderExt.mechKindDef.defName}");
            Pawn negotiator = TradeSession.playerNegotiator;
            for (int i = 0; i < countBought; i++)
            {
                USAC_MechTradeUtility.DropMech(mechOrderExt.mechKindDef, negotiator);
            }

            USAC_Debug.Log($"[USAC] Removing {countBought} orders from trader, thingsTrader.Count={__instance.thingsTrader.Count}");
            TransferableUtility.TransferNoSplit(__instance.thingsTrader, countBought, delegate (Thing thing, int countToTransfer)
            {
                USAC_Debug.Log($"[USAC] Destroying {countToTransfer}x {thing.def.defName}");
                thing.SplitOff(countToTransfer).Destroy();
            });

            USAC_Debug.Log("[USAC] Blocking original ResolveTrade");
            return false;
        }
    }

    // 重绘专用货币数值
    [HarmonyPatch(typeof(TradeUI), "DrawTradeableRow")]
    public static class Patch_TradeUI_DrawTradeableRow
    {
        public static void Postfix(Rect rect, Tradeable trad, int index)
        {
            // 仅处理USAC货币类型
            if (trad is not Tradeable_USACCurrency currency)
                return;

            // 确认当前是USAC交易
            if (!TradeSession.Active)
                return;

            var trader = TradeSession.trader;
            if (trader?.TraderKind == null)
                return;

            var ext = trader.TraderKind.GetModExtension<ModExtension_CorpseBagTrader>();
            if (ext == null || !ext.useCorpseBagCurrency)
                return;

            // 计算各项价值
            float corpseBagValue = 0f;
            int bondCount = 0;

            foreach (var thing in currency.thingsColony)
            {
                if (thing is Building_CorpseBag bag && bag.HasCorpse)
                    corpseBagValue += Building_CorpseBag.CalculateCorpseValue(bag.ContainedCorpse);
                else if (thing.def.defName == "USAC_Bond")
                    bondCount += thing.stackCount;
            }

            float bondValue = bondCount * 1000f;

            // 保存GUI状态
            var prevColor = GUI.color;
            var prevFont = Text.Font;
            var prevAnchor = Text.Anchor;
            bool prevWordWrap = Text.WordWrap;

            try
            {
                Text.Font = GameFont.Tiny;
                Text.WordWrap = false;

                // 准备文本
                string displayText;
                if (corpseBagValue > 0 && bondValue > 0)
                    displayText = $"{Mathf.RoundToInt(corpseBagValue)}+{Mathf.RoundToInt(bondValue)}";
                else if (bondValue > 0)
                    displayText = Mathf.RoundToInt(bondValue).ToString();
                else if (corpseBagValue > 0)
                    displayText = Mathf.RoundToInt(corpseBagValue).ToString();
                else
                    displayText = "0";

                // 动态计算渲染宽度
                float neededWidth = Text.CalcSize(displayText).x + 10f;
                float actualWidth = Mathf.Max(75f, neededWidth);

                // 绘制自定义数量显示
                Widgets.BeginGroup(rect);

                // 计算右侧边缘对齐点
                float rightEdge = rect.width - 175f - 240f - 100f;
                Rect countRect = new(rightEdge - actualWidth, 0f, actualWidth, rect.height);

                // 覆盖绘制深色背景
                GUI.color = new Color(0.12f, 0.12f, 0.12f, 1f);
                Widgets.DrawBoxSolid(countRect, GUI.color);
                GUI.color = Color.white;

                // 绘制文本
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(countRect, displayText);

                Widgets.EndGroup();
            }
            finally
            {
                // 恢复GUI状态
                GUI.color = prevColor;
                Text.Font = prevFont;
                Text.Anchor = prevAnchor;
                Text.WordWrap = prevWordWrap;
            }
        }
    }
}

