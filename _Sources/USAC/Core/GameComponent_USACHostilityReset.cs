using RimWorld;
using Verse;

namespace USAC
{
    // 执行周期性派系敌对关系重置
    public class GameComponent_USACHostilityReset : GameComponent
    {
        private int lastResetTick = -1;
        private const int TicksPerReset = 60000 * 10;

        public GameComponent_USACHostilityReset(Game game) : base()
        {
        }

        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % 250 != 0) return;
            if (Find.TickManager.TicksGame < lastResetTick + TicksPerReset)
                return;

            lastResetTick = Find.TickManager.TicksGame;
            TryResetHostility();
        }

        private void TryResetHostility()
        {
            Faction usacFaction = Find.FactionManager.FirstFactionOfDef(USAC_FactionDefOf.USAC_Faction);
            if (usacFaction == null)
                return;

            Faction playerFaction = Faction.OfPlayer;
            if (playerFaction == null)
                return;

            // 校验该派系与玩家是否为敌对
            if (!usacFaction.HostileTo(playerFaction))
                return;

            // 获取当前派系与玩家的好感度
            int currentGoodwill = usacFaction.GoodwillWith(playerFaction);

            // 强制负好感度回归至中立数值
            if (currentGoodwill < 0)
            {
                int goodwillChange = -currentGoodwill;
                usacFaction.TryAffectGoodwillWith(playerFaction, goodwillChange, false, false, USAC_HistoryEventDefOf.USAC_HostilityReset);
            }

            // 执行派系关系重置信件发送
            Find.LetterStack.ReceiveLetter(
                "USAC_HostilityReset".Translate(),
                "USAC_HostilityResetDesc".Translate(),
                LetterDefOf.NeutralEvent
            );
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref lastResetTick, "lastResetTick", -1);
        }
    }
}
