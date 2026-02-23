using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using Verse;

namespace USAC
{
    // 为据点清缴任务查找目标派系和站点部件
    public class QuestNode_USAC_GetEnemyFaction : QuestNode
    {
        public SlateRef<string> storeFactionAs;
        public SlateRef<string> storeSitePartsAs;
        public SlateRef<string> storeRelationAs;

        // 友好时中立目标的概率
        private const float NeutralTargetChance = 0.2f;

        protected override bool TestRunInt(Slate slate)
        {
            return TryResolve(slate, out _, out _, out _);
        }

        protected override void RunInt()
        {
            var slate = QuestGen.slate;
            if (!TryResolve(slate, out var faction, out var parts, out var rel))
                return;

            slate.Set(storeFactionAs.GetValue(slate), faction);
            slate.Set("sourceFaction", faction); // 记录原始派系
            slate.Set(storeSitePartsAs.GetValue(slate), parts);
            if (!storeRelationAs.GetValue(slate).NullOrEmpty())
                slate.Set(storeRelationAs.GetValue(slate), rel);

            // 供QuestNode_IsTrue使用
            slate.Set("isNeutralTarget", rel == "neutral");

            // 站点自定义标签
            slate.Set("siteLabel", PickSiteLabel(rel));

            if (faction == null || faction.Hidden) return;
            var questPart = new QuestPart_InvolvedFactions();
            questPart.factions.Add(faction);
            QuestGen.quest.AddPart(questPart);
        }

        private bool TryResolve(
            Slate slate, out Faction faction,
            out List<SitePartDef> parts, out string relation)
        {
            faction = null;
            parts = null;
            relation = "hostile";

            var usac = Find.FactionManager.FirstFactionOfDef(
                USAC_FactionDefOf.USAC_Faction);
            if (usac == null) return false;

            bool isAlly = usac.GoodwillWith(Faction.OfPlayer) >= 75;

            // 收集候选派系
            var hostileCandidates = new List<Faction>();
            var neutralCandidates = new List<Faction>();

            foreach (var f in Find.FactionManager.AllFactionsVisible)
            {
                if (f.IsPlayer || f == usac || f.defeated || f.temporary)
                    continue;

                if (isAlly)
                {
                    if (usac.HostileTo(f))
                        hostileCandidates.Add(f);
                    else
                        neutralCandidates.Add(f);
                }
                else
                {
                    // 中立时只选与两方都敌对的派系
                    if (usac.HostileTo(f) && f.HostileTo(Faction.OfPlayer))
                        hostileCandidates.Add(f);
                }
            }

            // 确定选择池
            bool pickNeutral = isAlly
                && neutralCandidates.Any()
                && Rand.Chance(NeutralTargetChance);

            List<Faction> pool;
            if (pickNeutral)
            {
                pool = neutralCandidates;
                relation = "neutral";
            }
            else if (hostileCandidates.Any())
            {
                pool = hostileCandidates;
                relation = "hostile";
            }
            else
            {
                return false;
            }

            // 根据关系选择站点部件标签
            string siteTag = pickNeutral ? "USAC_NeutralOutpost" : "BanditCamp";
            var campDefs = SiteMakerHelper
                .SitePartDefsWithTag(siteTag)?.ToList();
            if (campDefs == null || !campDefs.Any())
                return false;

            // 尝试匹配派系和站点部件
            float points = slate.Get("points", 0f);
            var validDefs = campDefs
                .Where(d => d.Worker.IsAvailable()
                    && points >= d.minThreatPoints)
                .ToList();
            if (!validDefs.Any())
                validDefs = campDefs
                    .Where(d => d.Worker.IsAvailable())
                    .ToList();
            if (!validDefs.Any())
                return false;

            // 筛选派系能拥有该站点部件的
            var validFactions = pool.Where(f =>
                validDefs.Any(d => d.FactionCanOwn(f))).ToList();
            if (!validFactions.Any())
                return false;

            var chosen = validFactions.RandomElement();
            faction = chosen;
            var factionDefs = validDefs
                .Where(d => d.FactionCanOwn(chosen)).ToList();
            parts = new List<SitePartDef>
                { factionDefs.RandomElement() };

            return true;
        }

        // 根据关系类型随机选择站点标签
        private string PickSiteLabel(string relation)
        {
            if (relation == "neutral")
            {
                var labels = new[]
                {
                    "USAC_SiteLabel_Resort".Translate(),
                    "USAC_SiteLabel_Retreat".Translate(),
                    "USAC_SiteLabel_Lodge".Translate(),
                    "USAC_SiteLabel_Outpost".Translate()
                };
                return labels.RandomElement();
            }
            else
            {
                var labels = new[]
                {
                    "USAC_SiteLabel_HostileOutpost".Translate(),
                    "USAC_SiteLabel_EnemyCamp".Translate(),
                    "USAC_SiteLabel_RogueBase".Translate(),
                    "USAC_SiteLabel_ForwardPost".Translate()
                };
                return labels.RandomElement();
            }
        }
    }
}
