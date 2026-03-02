using UnityEngine;
using Verse;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using static USAC.InternalUI.PortalUIUtility;

namespace USAC.InternalUI
{
    // 企业服务页面
    public class Page_Services : IPortalPage
    {
        public string Title => "USAC.UI.Services.Title".Translate();
        private Vector2 scrollPos;

        // 租赁参数状态
        private static int leaseDays = 3;
        private static bool leaseAutoRenew = false;

        private GameComponent_USACServices ServiceComp => Current.Game.GetComponent<GameComponent_USACServices>();

        public void Draw(Rect rect, Dialog_USACPortal parent)
        {
            float viewH = 500f;
            var serviceComp = ServiceComp;
            var map = Find.CurrentMap;

            var mechnitors = PawnsFinder.AllMaps_FreeColonistsSpawned
                .Where(p => p.health.hediffSet.HasHediff(DefDatabase<HediffDef>.GetNamed("USAC_TempMechlinkTrigger")))
                .ToList();

            var activeRigs = (map != null) ? map.listerBuildings.allBuildingsColonist.OfType<Building_HeavyMiningRig>().ToList() : new List<Building_HeavyMiningRig>();

            if (mechnitors.Count > 0) viewH += mechnitors.Count * 85f + 60f;
            if (activeRigs.Count > 0) viewH += activeRigs.Count * 85f + 60f;

            Widgets.BeginScrollView(rect, ref scrollPos, new Rect(0, 0, rect.width - 16, viewH));
            float y = 0;
            float w = rect.width - 16;

            // 租赁申请区块
            string priceStr = $"₿{leaseDays * 1000} / {leaseDays} DAYS";
            DrawLeaseConfigCard(ref y, w, priceStr, () => StartLease(parent));

            // 已投送资产监控
            if (activeRigs.Count > 0)
            {
                DrawRigSection(ref y, w, activeRigs);
            }

            // 远程链路续订
            DrawRenewalSection(ref y, w, mechnitors, serviceComp);

            Widgets.EndScrollView();
        }

        private void DrawLeaseConfigCard(ref float y, float w, string price, System.Action action)
        {
            Rect r = new(0, y, w, 200);
            DrawBentoBox(r, (box) =>
            {
                Rect inner = box.ContractedBy(20);
                DrawColoredLabel(inner.TopPartPixels(30), "USAC.UI.Services.Lease.Title".Translate().ToString().ToUpper(), ColAccentCamo1, GameFont.Small);
                DrawColoredLabel(new Rect(inner.x, inner.y + 35, inner.width - 180, 80), "USAC.UI.Services.Lease.Desc".Translate(), ColTextActive, GameFont.Tiny);

                // 天数步进调节器
                Rect selectR = new(inner.x, inner.yMax - 50, 200, 30);
                if (DrawTacticalButton(new Rect(selectR.x, selectR.y, 35, 30), "-")) leaseDays = Mathf.Max(1, leaseDays - 1);
                DrawColoredLabel(new Rect(selectR.x + 40, selectR.y, 110, 30), $"{leaseDays} DAYS", Color.white, GameFont.Tiny, TextAnchor.MiddleCenter);
                if (DrawTacticalButton(new Rect(selectR.x + 155, selectR.y, 35, 30), "+")) leaseDays = Mathf.Min(15, leaseDays + 1);

                // 统一自动续费开关
                Rect autoR = new(inner.x + 210, inner.yMax - 50, 140, 30);
                if (DrawTacticalButton(autoR, (leaseAutoRenew ? "ON" : "OFF") + " | AUTO", true, GameFont.Tiny)) leaseAutoRenew = !leaseAutoRenew;

                // 核心定价标签
                DrawColoredLabel(new Rect(inner.xMax - 165, inner.yMax - 70, 160, 20), price, ColTextMuted, GameFont.Tiny, TextAnchor.LowerRight);

                // 租赁执行按钮
                if (DrawTacticalButton(new Rect(inner.xMax - 160, inner.yMax - 45, 160, 45), "USAC.UI.Services.Lease.Btn".Translate(), true, GameFont.Small)) action?.Invoke();
            }, false);
            y += 220;
        }

        private void DrawRigSection(ref float y, float w, List<Building_HeavyMiningRig> rigs)
        {
            DrawColoredLabel(new Rect(0, y, w, 30), "USAC_ActiveRigs".Translate(), ColAccentCamo1, GameFont.Small);
            y += 40;
            foreach (var rig in rigs)
            {
                Rect r = new(0, y, w, 75);
                DrawBentoBox(r, (box) =>
                {
                    Rect inner = box.ContractedBy(12);
                    DrawColoredLabel(new Rect(inner.x, inner.y, 250, 25), rig.LabelCap, Color.white, GameFont.Small);
                    DrawColoredLabel(new Rect(inner.x, inner.y + 25, 250, 20), "USAC_LeaseActive".Translate(), ColTextMuted, GameFont.Tiny);

                    // 单机续费切换
                    Rect checkR = new(inner.xMax - 160, inner.y + 12, 150, 40);
                    if (DrawTacticalButton(checkR, (rig.autoRenew ? "ON" : "OFF") + " | AUTO", true, GameFont.Tiny)) rig.autoRenew = !rig.autoRenew;
                }, false);
                y += 85;
            }
        }

        private void StartLease(Dialog_USACPortal parent)
        {
            var debtComp = GameComponent_USACDebt.Instance;
            if (debtComp == null) return;

            if (debtComp.GetBondCountNearBeacons(Find.CurrentMap) < leaseDays)
            {
                Messages.Message("USAC.Message.InsufficientBondsForLease".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            debtComp.ConsumeBondsNearBeacons(Find.CurrentMap, leaseDays);
            int ticks = leaseDays * 60000;
            bool auto = leaseAutoRenew;
            parent.Close();

            Find.Targeter.BeginTargeting(
                new TargetingParameters { canTargetLocations = true },
                delegate (LocalTargetInfo target)
                {
                    if (target.Cell.InBounds(Find.CurrentMap))
                    {
                        ThingDef incomingDef = DefDatabase<ThingDef>.GetNamed("USAC_MiningRigIncoming");
                        var skyfaller = (Skyfaller_MiningRigIncoming)ThingMaker.MakeThing(incomingDef);
                        skyfaller.SetLease(ticks, auto);
                        skyfaller.SetGuards(null, Faction.OfPlayer);
                        GenSpawn.Spawn(skyfaller, target.Cell, Find.CurrentMap);
                    }
                },
                null,
                null,
                null,
                null,
                null,
                true,
                null,
                delegate (LocalTargetInfo target)
                {
                    if (target.Cell.IsValid && target.Cell.InBounds(Find.CurrentMap))
                    {

                        GenDraw.DrawRadiusRing(target.Cell, 6.5f);
                        Find.CurrentMap.deepResourceGrid.MarkForDraw();
                        GenDraw.DrawTargetHighlight(target);
                    }
                }
            );
        }

        private void DrawRenewalSection(ref float y, float w, List<Pawn> mechnitors, GameComponent_USACServices comp)
        {
            DrawColoredLabel(new Rect(0, y, w, 30), "USAC.UI.Services.Renewal.Header".Translate(), ColAccentCamo1, GameFont.Small);
            y += 40;

            if (mechnitors.Count == 0)
            {
                DrawColoredLabel(new Rect(10, y, w, 30), "USAC.UI.Services.Renewal.NoMechnitor".Translate(), ColTextMuted, GameFont.Tiny);
                y += 40;
                return;
            }

            foreach (var pawn in mechnitors)
            {
                Rect r = new(0, y, w, 75);
                DrawBentoBox(r, (box) =>
                {
                    Rect inner = box.ContractedBy(12);
                    var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(DefDatabase<HediffDef>.GetNamed("USAC_TempMechlinkTrigger"));
                    string time = hediff.TryGetComp<HediffComp_Disappears>()?.ticksToDisappear.ToStringTicksToPeriod() ?? "0";

                    DrawColoredLabel(new Rect(inner.x, inner.y, 250, 25), pawn.LabelShort, Color.white, GameFont.Small);
                    DrawColoredLabel(new Rect(inner.x, inner.y + 25, 250, 20), "USAC.UI.Services.Renewal.Remaining".Translate(time), ColTextMuted, GameFont.Tiny);

                    // 带宽续费开关
                    bool auto = comp.autoRenewPawns.Contains(pawn);
                    Rect checkR = new(inner.xMax - 325, inner.y + 12, 110, 30);
                    if (DrawTacticalButton(checkR, (auto ? "ON" : "OFF") + " | AUTO", true, GameFont.Tiny, key: $"auto_{pawn.thingIDNumber}"))
                    {
                        if (auto) comp.autoRenewPawns.Remove(pawn);
                        else comp.autoRenewPawns.Add(pawn);
                    }

                    // 续费价格标识
                    Rect priceR = new(inner.xMax - 210, inner.y + 12, 60, 30);
                    DrawColoredLabel(priceR, "₿4,000", ColTextMuted, GameFont.Tiny, TextAnchor.MiddleRight);

                    // 远程续期执行
                    if (DrawTacticalButton(new Rect(inner.xMax - 145, inner.y + 5, 145, 45), "USAC.UI.Services.Renewal.Btn".Translate(), true, GameFont.Tiny, key: $"renew_{pawn.thingIDNumber}"))
                    {
                        var debtComp = GameComponent_USACDebt.Instance;
                        if (debtComp != null && debtComp.GetBondCountNearBeacons(pawn.Map) >= 4)
                        {
                            debtComp.ConsumeBondsNearBeacons(pawn.Map, 4);
                            var disappear = hediff.TryGetComp<HediffComp_Disappears>();
                            if (disappear != null) disappear.ticksToDisappear += 1800000;
                            Messages.Message("USAC.Message.Renewed".Translate(pawn.LabelShort), pawn, MessageTypeDefOf.PositiveEvent);
                        }
                        else
                        {
                            Messages.Message("USAC.Message.InsufficientBonds".Translate(), MessageTypeDefOf.RejectInput);
                        }
                    }

                }, false);
                y += 85;
            }
        }
    }
}
