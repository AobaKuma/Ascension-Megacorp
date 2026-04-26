using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace USAC
{
    // 债务据点生成器
    public class DebtSiteGenerator : IExposable
    {
        #region 存档
        public void ExposeData()
        {
            // 无需持久化字段
        }
        #endregion

        #region 据点生成
        public void TryGenerateDebtSite(DebtContract contract, Map map)
        {
            PlanetTile siteTile;
            PlanetTile nearTile = Find.RandomPlayerHomeMap?.Tile ?? PlanetTile.Invalid;

            if (!TileFinder.TryFindNewSiteTile(out siteTile, nearTile, 4, 12, true, null, 0.5f, true, TileFinderMode.Random, false, false))
                return;

            var siteDef = SelectSiteType();
            if (siteDef == null) return;

            Log.Message($"[USAC] 为订单 {contract.Label} 随机选择了据点类型: {siteDef.defName}");

            var sitePartDefWithParams = new SitePartDefWithParams(siteDef, new SitePartParams());
            var parts = new List<SitePartDefWithParams> { sitePartDefWithParams };

            Faction faction = Find.FactionManager.RandomEnemyFaction(false, false, true, TechLevel.Industrial);
            Site site = (Site)WorldObjectMaker.MakeWorldObject(USAC_DefOf.USAC_DebtSite);
            site.Tile = siteTile;
            site.SetFaction(faction);
            foreach (var part in parts)
            {
                site.AddPart(new SitePart(site, part.def, part.parms));
            }

            Find.WorldObjects.Add(site);

            Find.LetterStack.ReceiveLetter(
                "USAC_DebtSite_LetterLabel".Translate(),
                "USAC_DebtSite_LetterText".Translate(contract.Label),
                LetterDefOf.ThreatBig, site);
        }

        public void GenerateSiteBatch(DebtContract contract, Map map, int count)
        {
            if (contract == null || map == null) return;

            for (int i = 0; i < count; i++)
            {
                TryGenerateDebtSite(contract, map);
            }
        }

        // 选择据点类型
        private SitePartDef SelectSiteType()
        {
            var choices = new List<SitePartDef>();
            if (USAC_DefOf.USAC_CommercialOutpost != null) choices.Add(USAC_DefOf.USAC_CommercialOutpost);
            if (USAC_DefOf.USAC_ArtilleryPosition != null) choices.Add(USAC_DefOf.USAC_ArtilleryPosition);
            if (USAC_DefOf.USAC_Airbase != null) choices.Add(USAC_DefOf.USAC_Airbase);

            if (choices.Count == 0) return null;
            return choices.RandomElement();
        }
        #endregion
    }
}
