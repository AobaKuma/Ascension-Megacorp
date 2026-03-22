using System;
using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace USAC.Endings
{
    // 处理防御胜利流程
    public class GameComponent_DebtTransfer : GameComponent
    {
        #region 阶段定义
        public enum TransferPhase
        {
            None,
            Negotiation,     // 谈判期
            Countdown,       // 备战倒计时
            UnderSiege,      // 正在防守
            Resolved         // 已解决
        }
        #endregion

        #region 字段
        public TransferPhase Phase = TransferPhase.None;
        public Faction BuyerFaction;
        public float OriginalDebtAmount;
        public int SilverBuyout;
        public int CountdownTargetTick;
        public int SiegeStartTick;
        public int SiegeDaysRequired;
        public int WavesLaunched;
        public int NextWaveTick;

        private const int CountdownDays = 15;
        // 防守 10 天
        private const int SiegeDaysConst = 10;
        private const int WaveIntervalTicks = (int)(GenDate.TicksPerDay * 1.2f);
        #endregion

        public GameComponent_DebtTransfer(Game game) { }

        public static GameComponent_DebtTransfer Instance =>
            Current.Game?.GetComponent<GameComponent_DebtTransfer>();

        #region 生命周期
        public override void GameComponentTick()
        {
            if (Phase == TransferPhase.None || Phase == TransferPhase.Resolved || Phase == TransferPhase.Negotiation)
                return;

            int now = Find.TickManager.TicksGame;

            if (Phase == TransferPhase.Countdown)
            {
                if (now >= CountdownTargetTick)
                    StartSiege();
            }
            else if (Phase == TransferPhase.UnderSiege)
            {
                if (now >= NextWaveTick)
                    LaunchWave();

                if (now % 500 == 0)
                    CheckSiegeComplete(now);
            }
        }
        #endregion

        #region 触发入口
        public static void TriggerEnding(DebtContract contract)
        {
            var inst = Instance;
            if (inst == null || inst.Phase != TransferPhase.None) return;

            // 渲染独立图标
            inst.BuyerFaction = PickBuyerFaction();
            if (inst.BuyerFaction == null) return;

            var debtComp = GameComponent_USACDebt.Instance;
            inst.OriginalDebtAmount = debtComp?.TotalDebt ?? 0f;
            inst.SilverBuyout = Mathf.CeilToInt(inst.OriginalDebtAmount * 1.5f);

            // 放置完成
            ClearUSACDebt(debtComp);

            inst.Phase = TransferPhase.Negotiation;

            // 弹出谈判弹窗
            LongEventHandler.ExecuteWhenFinished(() => ShowNegotiationDialog(inst));
        }

        private static void ShowNegotiationDialog(GameComponent_DebtTransfer inst)
        {
            int silver = inst.SilverBuyout;
            string text = "USAC.Ending.Transfer.NegotiationText"
                .Translate(inst.BuyerFaction.Name, inst.OriginalDebtAmount.ToString("N0"), silver);

            DiaNode node = new DiaNode(text);

            // 赎买方案
            DiaOption optPay = new DiaOption("USAC.Ending.Transfer.Option.Pay".Translate(silver))
            {
                action = () => TryBuyout(inst, silver),
                resolveTree = true
            };

            // 资产汇总行
            DiaOption optRefuse = new DiaOption("USAC.Ending.Transfer.Option.Refuse".Translate(CountdownDays))
            {
                action = () => StartCountdown(inst),
                resolveTree = true
            };

            node.options.Add(optPay);
            node.options.Add(optRefuse);

            Find.WindowStack.Add(new Dialog_NodeTree(node, true, false, "USAC.Ending.Transfer.NegotiationTitle".Translate(inst.BuyerFaction.Name)));
        }
        #endregion

        #region 逻辑分支
        private static void TryBuyout(GameComponent_DebtTransfer inst, int silverNeeded)
        {
            Map map = GameComponent_USACDebt.GetRichestPlayerHomeMap();
            if (map == null) return;

            int silverCount = 0;
            var silvers = map.listerThings.ThingsOfDef(ThingDefOf.Silver);
            for (int i = 0; i < silvers.Count; i++) silverCount += silvers[i].stackCount;

            if (silverCount < silverNeeded)
            {
                Messages.Message("USAC.Ending.Transfer.BuyoutFail".Translate(silverCount, silverNeeded), MessageTypeDefOf.RejectInput);
                StartCountdown(inst);
                return;
            }

            // 扣除白银
            int remaining = silverNeeded;
            for (int i = silvers.Count - 1; i >= 0 && remaining > 0; i--)
            {
                var s = silvers[i];
                int take = Math.Min(remaining, s.stackCount);
                s.SplitOff(take).Destroy();
                remaining -= take;
            }

            inst.Phase = TransferPhase.Resolved;
            Messages.Message("USAC.Ending.Transfer.BuyoutSuccess".Translate(inst.BuyerFaction.Name), MessageTypeDefOf.PositiveEvent);
        }

        private static void StartCountdown(GameComponent_DebtTransfer inst)
        {
            inst.Phase = TransferPhase.Countdown;
            inst.CountdownTargetTick = Find.TickManager.TicksGame + CountdownDays * GenDate.TicksPerDay;
            inst.SiegeDaysRequired = SiegeDaysConst;

            Find.LetterStack.ReceiveLetter(
                "USAC.Ending.Transfer.CountdownLabel".Translate(),
                "USAC.Ending.Transfer.CountdownText".Translate(inst.BuyerFaction.Name, CountdownDays),
                LetterDefOf.ThreatBig);
        }

        private void StartSiege()
        {
            Phase = TransferPhase.UnderSiege;
            SiegeStartTick = Find.TickManager.TicksGame;
            WavesLaunched = 0;
            NextWaveTick = SiegeStartTick;

            Find.LetterStack.ReceiveLetter(
                "USAC.Ending.Transfer.SiegeStartLabel".Translate(),
                "USAC.Ending.Transfer.SiegeStartText".Translate(BuyerFaction.Name, SiegeDaysRequired),
                LetterDefOf.ThreatBig);
        }

        private void LaunchWave()
        {
            Map map = GameComponent_USACDebt.GetRichestPlayerHomeMap();
            if (map == null) return;

            // 子波次袭击 周期性触发
            int raidCount = Rand.RangeInclusive(2, 5);
            bool launchedAny = false;

            for (int i = 0; i < raidCount; i++)
            {
                // 强度随波次提升
                float points = StorytellerUtility.DefaultThreatPointsNow(map) * (1.2f + WavesLaunched * 0.25f);
                points = Mathf.Max(1000f, points);

                IncidentParms parms = new IncidentParms
                {
                    target = map,
                    points = points,
                    faction = BuyerFaction,
                    forced = true,
                    // 绘制文本 带左边距波步行
                    raidArrivalMode = (i == 0) ? PawnsArrivalModeDefOf.EdgeWalkIn : PawnsArrivalModeDefOf.RandomDrop
                };

                if (IncidentDefOf.RaidEnemy.Worker.TryExecute(parms))
                {
                    launchedAny = true;
                }
            }

            if (launchedAny)
            {
                WavesLaunched++;
                NextWaveTick = Find.TickManager.TicksGame + WaveIntervalTicks;
            }
            else
            {
                // 底部横穿全宽区域时重试
                NextWaveTick = Find.TickManager.TicksGame + 2500;
            }
        }

        private void CheckSiegeComplete(int now)
        {
            int elapsed = now - SiegeStartTick;
            if (elapsed < SiegeDaysRequired * GenDate.TicksPerDay) return;

            Map map = GameComponent_USACDebt.GetRichestPlayerHomeMap();
            if (map == null) return;

            bool hostileStillPresent = false;
            var list = map.mapPawns.SpawnedPawnsInFaction(BuyerFaction);
            for (int i = 0; i < list.Count; i++)
            {
                if (!list[i].Dead && !list[i].Downed && (list[i].MentalStateDef == null || !list[i].MentalStateDef.IsExtreme))
                {
                    hostileStillPresent = true;
                    break;
                }
            }

            if (!hostileStillPresent)
                CompleteSiege();
        }

        private void CompleteSiege()
        {
            Phase = TransferPhase.Resolved;
            ShowVictoryCredits();
            Find.LetterStack.ReceiveLetter(
                "USAC.Ending.Transfer.VictoryLabel".Translate(),
                "USAC.Ending.Transfer.VictoryText".Translate(BuyerFaction.Name),
                LetterDefOf.PositiveEvent);
        }
        #endregion

        #region 辅助方法
        private void ShowVictoryCredits()
        {
            var sb = new StringBuilder();
            sb.AppendLine("USAC.Ending.Transfer.Victory.Title".Translate());
            sb.AppendLine();
            sb.AppendLine("USAC.Ending.Transfer.Victory.Body".Translate());

            string full = GameVictoryUtility.MakeEndCredits(sb.ToString(), "USAC.Ending.Transfer.Victory.Close".Translate(), string.Empty);
            GameVictoryUtility.ShowCredits(full, null, exitToMainMenu: false);
        }

        private static Faction PickBuyerFaction()
        {
            foreach (Faction f in Find.FactionManager.AllFactions)
            {
                if (!f.IsPlayer && f.def != USAC_FactionDefOf.USAC_Faction && f.def.techLevel >= TechLevel.Industrial && f.HostileTo(Faction.OfPlayer))
                    return f;
            }
            return Find.FactionManager.RandomEnemyFaction(minTechLevel: TechLevel.Industrial);
        }

        private static void ClearUSACDebt(GameComponent_USACDebt comp)
        {
            if (comp?.ActiveContracts == null) return;
            foreach (var c in comp.ActiveContracts)
            {
                c.IsActive = false;
                c.Principal = 0;
                c.AccruedInterest = 0;
            }
            comp.IsSystemLocked = true;
        }
        #endregion

        #region 存档
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref Phase, "TransferPhase", TransferPhase.None);
            Scribe_References.Look(ref BuyerFaction, "BuyerFaction");
            Scribe_Values.Look(ref OriginalDebtAmount, "OriginalDebtAmount", 0f);
            Scribe_Values.Look(ref SilverBuyout, "SilverBuyout", 0);
            Scribe_Values.Look(ref CountdownTargetTick, "CountdownTargetTick", 0);
            Scribe_Values.Look(ref SiegeStartTick, "SiegeStartTick", 0);
            Scribe_Values.Look(ref SiegeDaysRequired, "SiegeDaysRequired", 0);
            Scribe_Values.Look(ref WavesLaunched, "WavesLaunched", 0);
            Scribe_Values.Look(ref NextWaveTick, "NextWaveTick", 0);
        }
        #endregion
    }
}
