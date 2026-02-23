using RimWorld;
using RimWorld.QuestGen;
using Verse;

namespace USAC
{
    // 检查USAC与玩家的关系
    public class QuestNode_USAC_IsAllyToPlayer : QuestNode
    {
        public QuestNode node;
        public QuestNode elseNode;

        protected override bool TestRunInt(Slate slate)
        {
            if (IsAlly())
                return node?.TestRun(slate) ?? true;
            return elseNode?.TestRun(slate) ?? true;
        }

        protected override void RunInt()
        {
            if (IsAlly())
                node?.Run();
            else
                elseNode?.Run();
        }

        // 检查USAC是否与玩家友好
        private bool IsAlly()
        {
            var faction = Find.FactionManager.FirstFactionOfDef(
                USAC_FactionDefOf.USAC_Faction);
            if (faction == null) return false;
            return faction.GoodwillWith(Faction.OfPlayer) >= 75;
        }
    }
}
