using RimWorld;
using Verse;

namespace USAC
{
    // 派系商船管理组件
    // 年度固定频次商船到访
    public class GameComponent_USACTrader : GameComponent
    {
        #region 常量

        // 年度总天数
        private const int DaysPerYear = 60;

        // 年度商船到访次数
        private const int VisitsPerYear = 2;

        // 单次到访间隔天数
        private const int DaysBetweenVisits = DaysPerYear / VisitsPerYear;

        // 首次到访最短天数
        private const int MinDaysForFirstVisit = 15;

        // 到访日期随机偏移
        private const int RandomOffsetDays = 5;

        #endregion

        #region 字段

        // 下次商船到访时间
        private int nextVisitTick = -1;

        #endregion

        #region 构造函数

        public GameComponent_USACTrader(Game game)
        {
        }

        #endregion

        #region 生命周期

        public override void StartedNewGame()
        {
            base.StartedNewGame();
            ScheduleNextVisit(true);
        }

        public override void LoadedGame()
        {
            base.LoadedGame();

            // 补录缺失计划
            if (nextVisitTick < 0)
            {
                ScheduleNextVisit(false);
            }
        }

        public override void GameComponentTick()
        {
            // 商船到访计划检验
            if (Find.TickManager.TicksGame % 250 != 0)
                return;

            if (nextVisitTick > 0 && Find.TickManager.TicksGame >= nextVisitTick)
            {
                TryTriggerVisit();
                ScheduleNextVisit(false);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref nextVisitTick, "nextVisitTick", -1);
        }

        #endregion

        #region 公开接口

        #endregion

        #region 私有方法

        private void ScheduleNextVisit(bool isFirstVisit)
        {
            int baseDays = isFirstVisit ? MinDaysForFirstVisit : DaysBetweenVisits;
            int randomOffset = Rand.RangeInclusive(-RandomOffsetDays, RandomOffsetDays);
            int daysUntilVisit = baseDays + randomOffset;

            nextVisitTick = Find.TickManager.TicksGame + (daysUntilVisit * GenDate.TicksPerDay);
        }

        private void TryTriggerVisit()
        {
            // 检查派系存续状态
            Faction usacFaction = Find.FactionManager.FirstFactionOfDef(USAC_FactionDefOf.USAC_Faction);
            if (usacFaction == null || usacFaction.HostileTo(Faction.OfPlayer))
                return;

            // 获取玩家主基地地图
            Map map = Find.AnyPlayerHomeMap;
            if (map == null)
                return;

            // 检查地图商船数量
            if (map.passingShipManager.passingShips.Count >= 5)
                return;

            // 触发派系商船到访
            IncidentDef incidentDef = DefDatabase<IncidentDef>.GetNamedSilentFail("USAC_MechSupplierArrival");
            if (incidentDef == null)
                return;

            IncidentParms parms = StorytellerUtility.DefaultParmsNow(incidentDef.category, map);
            incidentDef.Worker.TryExecute(parms);
        }

        #endregion
    }
}
