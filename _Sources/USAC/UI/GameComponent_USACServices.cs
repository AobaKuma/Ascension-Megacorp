using System.Collections.Generic;
using Verse;
using RimWorld;

namespace USAC
{
    // USAC 企业服务管理
    public class GameComponent_USACServices : GameComponent
    {
        #region 字段
        // 自动续费名单
        public HashSet<Pawn> autoRenewPawns = new HashSet<Pawn>();

        // 轨道商船预约到达时刻
        public int traderArrivalTick = -1;

        // 复用移除列表避免每次分配
        private static readonly List<Pawn> tmpRemove = new List<Pawn>();
        #endregion

        #region 生命周期
        public GameComponent_USACServices(Game game) { }

        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % 2500 == 0)
            {
                CheckAutoRenewals();
            }

            // 检测商船预约到期
            if (traderArrivalTick > 0 && Find.TickManager.TicksGame >= traderArrivalTick)
            {
                SpawnScheduledTrader();
                traderArrivalTick = -1;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref autoRenewPawns, "autoRenewPawns", LookMode.Reference);
            if (autoRenewPawns == null) autoRenewPawns = new HashSet<Pawn>();
            Scribe_Values.Look(ref traderArrivalTick, "traderArrivalTick", -1);
        }
        #endregion

        #region 逻辑
        private void CheckAutoRenewals()
        {
            tmpRemove.Clear();
            foreach (var pawn in autoRenewPawns)
            {
                if (pawn == null || pawn.Dead || pawn.Destroyed)
                {
                    tmpRemove.Add(pawn);
                    continue;
                }

                // 直接使用 DefOf 静态引用
                var trigger = pawn.health.hediffSet
                    .GetFirstHediffOfDef(USAC_DefOf.USAC_TempMechlinkTrigger) as HediffWithComps;
                if (trigger == null)
                {
                    tmpRemove.Add(pawn);
                    continue;
                }

                var disappearComp = trigger.TryGetComp<HediffComp_Disappears>();
                if (disappearComp != null && disappearComp.ticksToDisappear < 2550)
                    TryRenew(pawn, disappearComp);
            }

            for (int i = 0; i < tmpRemove.Count; i++)
                autoRenewPawns.Remove(tmpRemove[i]);
        }

        public void TryRenew(Pawn pawn, HediffComp_Disappears comp)
        {
            var debtComp = GameComponent_USACDebt.Instance;
            if (debtComp != null && debtComp.GetBondCountNearBeacons(pawn.Map) >= 4)
            {
                debtComp.ConsumeBondsNearBeacons(pawn.Map, 4);
                comp.ticksToDisappear += 1800000;
                Messages.Message("USAC.Message.AutoRenewed".Translate(pawn.LabelShort), pawn, MessageTypeDefOf.PositiveEvent);
            }
            else
            {
                // 扣费失败取消续费
                autoRenewPawns.Remove(pawn);
                Messages.Message("USAC.Message.AutoRenewFailed".Translate(pawn.LabelShort), pawn, MessageTypeDefOf.NegativeEvent);
            }
        }

        // 刷新预约的轨道商船
        private void SpawnScheduledTrader()
        {
            Map map = Find.AnyPlayerHomeMap;
            if (map == null) return;

            // 使用DefOf静态引用
            Faction usacFaction = Find.FactionManager
                .FirstFactionOfDef(USAC_FactionDefOf.USAC_Faction);
            if (usacFaction == null || usacFaction.HostileTo(Faction.OfPlayer)) return;

            if (map.passingShipManager.passingShips.Count >= 5) return;

            if (USAC_DefOf.USAC_Trader_Orbital == null) return;

            TradeShip ship = new TradeShip(USAC_DefOf.USAC_Trader_Orbital, usacFaction);
            map.passingShipManager.AddShip(ship);
            ship.GenerateThings();

            Messages.Message("USAC.Message.TraderArrived".Translate(), MessageTypeDefOf.PositiveEvent);
        }
        #endregion
    }
}
