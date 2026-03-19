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
            tradeables.RemoveAll(t => t.ThingDef == ThingDefOf.Silver && t.IsCurrency);

            ThingDef corpseBagDef = DefDatabase<ThingDef>.GetNamedSilentFail(ext.corpseBagDefName);
            ThingDef bondDef = USAC_DefOf.USAC_Bond;

            var currency = new Tradeable_USACCurrency();
            Map map = TradeSession.playerNegotiator.Map;

            if (map == null)
            {
                tradeables.Add(currency);
                return;
            }

            HashSet<Thing> addedThings = new();

            foreach (var beacon in Building_OrbitalTradeBeacon.AllPowered(map))
            {
                foreach (var cell in beacon.TradeableCells)
                {
                    List<Thing> thingList = cell.GetThingList(map);
                    foreach (var thing in thingList)
                    {
                        if (addedThings.Contains(thing))
                            continue;

                        if (corpseBagDef != null && thing is Building_CorpseBag bag &&
                            bag.def == corpseBagDef && bag.HasCorpse &&
                            bag.Faction == Faction.OfPlayer && !bag.IsForbidden(Faction.OfPlayer))
                        {
                            currency.AddThing(bag, Transactor.Colony);
                            addedThings.Add(thing);
                        }
                        else if (thing is Building_USACCorpseStorage storage &&
                            !storage.IsEmpty &&
                            storage.Faction == Faction.OfPlayer && !storage.IsForbidden(Faction.OfPlayer))
                        {
                            currency.AddThing(storage, Transactor.Colony);
                            addedThings.Add(storage);
                        }
                        else if (thing.def == bondDef && thing.def.category == ThingCategory.Item)
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
            ThingDef bondDef = USAC_DefOf.USAC_Bond;
            if (bondDef == null)
                return;

            tradeables.RemoveAll(t =>
                t.ThingDef == bondDef
                && t is not Tradeable_Bond
                && t is not Tradeable_USACCurrency);

            var bondTradeable = new Tradeable_Bond();

            // 扫描商人债券库存
            foreach (var thing in TradeSession.trader.Goods)
            {
                if (thing.def == bondDef)
                    bondTradeable.AddThing(thing, Transactor.Trader);
            }

            // 商人持有债券时才显示购买入口
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

            __result = null;
            return false;
        }
    }

    // 触发成交后机兵空投
    [HarmonyPatch(typeof(Tradeable), nameof(Tradeable.ResolveTrade))]
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


    // 固定价格不受结算影响
    [HarmonyPatch(typeof(Tradeable), nameof(Tradeable.GetPriceFor))]
    public static class Patch_USAC_CorpseStorageTradePrice
    {
        public static bool Prefix(Tradeable __instance, ref float __result)
        {
            if (__instance.ThingDef?.defName == "USAC_CorpseStorage")
            {
                __result = __instance.AnyThing.MarketValue;
                return false;
            }
            return true;
        }
    }
}

