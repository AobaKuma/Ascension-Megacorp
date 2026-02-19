using RimWorld;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;

namespace USAC
{
    // 定义尸体袋建筑逻辑啊
    // 记录派系贸易交易属性
    // 扩展存储搬运接口逻辑
    public class Building_CorpseBag : Building_Casket, IHaulDestination, IStoreSettingsParent, IHaulEnroute, INotifyHauledTo
    {
        #region 常量

        private const float CorpseValueFactor = 0.5f;

        #endregion

        #region 字段

        private float cachedMarketValue = -1f;
        private Graphic cachedGraphicEmpty;
        private Graphic cachedGraphicFull;
        private StorageSettings storageSettings;

        #endregion

        #region 属性

        public ModExtension_CorpseBag Extension => def.GetModExtension<ModExtension_CorpseBag>();

        public Corpse ContainedCorpse
        {
            get
            {
                for (int i = 0; i < innerContainer.Count; i++)
                {
                    if (innerContainer[i] is Corpse corpse)
                        return corpse;
                }
                return null;
            }
        }

        public bool HasCorpse => ContainedCorpse != null;

        // 检查存储标签可见性
        public bool StorageTabVisible => !HasCorpse;

        // 检查搬运目的地状态
        public bool HaulDestinationEnabled => true;

        public override Graphic Graphic
        {
            get
            {
                if (HasCorpse)
                {
                    if (cachedGraphicFull == null && Extension?.graphicDataFull != null)
                        cachedGraphicFull = Extension.graphicDataFull.GraphicColoredFor(this);
                    return cachedGraphicFull ?? base.Graphic;
                }
                else
                {
                    if (cachedGraphicEmpty == null && Extension?.graphicDataEmpty != null)
                        cachedGraphicEmpty = Extension.graphicDataEmpty.GraphicColoredFor(this);
                    return cachedGraphicEmpty ?? base.Graphic;
                }
            }
        }

        public override float MarketValue
        {
            get
            {
                if (cachedMarketValue < 0f)
                    cachedMarketValue = CalculateMarketValue();
                return cachedMarketValue;
            }
        }

        #endregion

        #region 存储设置接口

        public StorageSettings GetStoreSettings()
        {
            return storageSettings;
        }

        public StorageSettings GetParentStoreSettings()
        {
            return def.building.fixedStorageSettings;
        }

        public void Notify_SettingsChanged()
        {
        }

        public int SpaceRemainingFor(ThingDef _)
        {
            if (!HasCorpse)
                return 1;
            return 0;
        }

        #endregion

        #region 生命周期

        public override void PostMake()
        {
            base.PostMake();
            storageSettings = new StorageSettings(this);
            if (def.building.defaultStorageSettings != null)
                storageSettings.CopyFrom(def.building.defaultStorageSettings);
        }

        public override void TickRare()
        {
            base.TickRare();

            // 刷新市值缓存
            if (HasCorpse)
            {
                InvalidateMarketValueCache();
            }
        }

        #endregion

        #region 公共方法

        private float CalculateMarketValue()
        {
            Corpse corpse = ContainedCorpse;
            if (corpse == null)
                return def.BaseMarketValue;

            return CalculateCorpseValue(corpse) + def.BaseMarketValue;
        }

        public static float CalculateCorpseValue(Corpse corpse)
        {
            if (corpse == null)
                return 0f;

            // 校验腐烂尸体价值
            CompRottable rottable = corpse.TryGetComp<CompRottable>();
            if (rottable != null)
            {
                RotStage stage = rottable.Stage;
                if (stage == RotStage.Rotting || stage == RotStage.Dessicated)
                    return 0f;
            }

            Pawn innerPawn = corpse.InnerPawn;
            if (innerPawn == null)
                return 0f;

            float baseValue = innerPawn.def.GetStatValueAbstract(StatDefOf.MarketValue);
            float qualityFactor = CalculateLivingQualityFactor(innerPawn);
            float qualityOffset = PriceUtility.PawnQualityPriceOffset(innerPawn);

            float livingValue = baseValue * qualityFactor + qualityOffset;

            float durabilityRatio = 1f;
            if (corpse.MaxHitPoints > 0)
                durabilityRatio = (float)corpse.HitPoints / corpse.MaxHitPoints;

            return livingValue * CorpseValueFactor * durabilityRatio;
        }

        private static float CalculateLivingQualityFactor(Pawn pawn)
        {
            float factor = 1f;

            if (pawn.skills != null)
            {
                float avgSkill = 0f;
                int count = 0;
                foreach (var skill in pawn.skills.skills)
                {
                    avgSkill += skill.Level;
                    count++;
                }
                if (count > 0)
                {
                    avgSkill /= count;
                    if (avgSkill <= 5.5f)
                        factor *= Mathf.Lerp(0.2f, 1f, avgSkill / 5.5f);
                    else
                        factor *= Mathf.Lerp(1f, 3f, (avgSkill - 5.5f) / 14.5f);
                }
            }

            if (pawn.ageTracker?.CurLifeStage != null)
                factor *= pawn.ageTracker.CurLifeStage.marketValueFactor;

            if (pawn.story?.traits != null)
            {
                foreach (var trait in pawn.story.traits.allTraits)
                {
                    if (!trait.Suppressed)
                        factor += trait.CurrentData.marketValueFactorOffset;
                }
            }

            factor += pawn.GetStatValue(StatDefOf.PawnBeauty) * 0.2f;
            factor += CalculateResearchValueBonus(pawn);

            if (factor < 0.1f)
                factor = 0.1f;

            return factor;
        }

        private static float CalculateResearchValueBonus(Pawn pawn)
        {
            float bonus = 0f;

            if (pawn.health?.hediffSet == null)
                return bonus;

            int rareConditionCount = 0;
            int addictionCount = 0;

            foreach (var hediff in pawn.health.hediffSet.hediffs)
            {
                if (hediff is Hediff_Addiction)
                {
                    addictionCount++;
                    continue;
                }

                if (IsRareCondition(hediff))
                    rareConditionCount++;
            }

            bonus += Mathf.Min(addictionCount * 0.03f, 0.15f);
            bonus += Mathf.Min(rareConditionCount * 0.05f, 0.20f);

            return bonus;
        }

        private static bool IsRareCondition(Hediff hediff)
        {
            if (hediff?.def == null)
                return false;

            if (hediff.def.defName.Contains("Carcinoma") || hediff.def.defName.Contains("Cancer"))
                return true;

            if (hediff.def.chronic)
                return true;

            if (hediff.def.hediffClass == typeof(Hediff_MissingPart))
                return false;

            if (hediff.def.tags != null)
            {
                foreach (var tag in hediff.def.tags)
                {
                    if (tag == "ChronicDisease" || tag == "GeneticDisease")
                        return true;
                }
            }

            string defName = hediff.def.defName;
            if (defName == "Alzheimers" || defName == "Dementia" ||
                defName == "Asthma" || defName == "BadBack" ||
                defName == "Frail" || defName == "Cataract" ||
                defName == "HearingLoss" || defName == "Blindness")
                return true;

            return false;
        }

        public bool CanAcceptCorpse(Corpse corpse)
        {
            if (corpse == null)
                return false;

            if (HasCorpse)
                return false;

            CompRottable rottable = corpse.TryGetComp<CompRottable>();
            if (rottable != null)
            {
                RotStage stage = rottable.Stage;
                if (stage == RotStage.Rotting || stage == RotStage.Dessicated)
                    return false;
            }

            return true;
        }

        public override bool Accepts(Thing thing)
        {
            if (!base.Accepts(thing))
                return false;

            if (HasCorpse)
                return false;

            if (thing is Corpse corpse)
            {
                if (!CanAcceptCorpse(corpse))
                    return false;

                if (!storageSettings.AllowedToAccept(thing))
                    return false;

                return true;
            }

            return false;
        }

        public override bool TryAcceptThing(Thing thing, bool allowSpecialEffects = true)
        {
            bool result = base.TryAcceptThing(thing, allowSpecialEffects);
            if (result)
            {
                InvalidateGraphicCache();
                InvalidateMarketValueCache();
            }
            return result;
        }

        public override void EjectContents()
        {
            base.EjectContents();
            InvalidateGraphicCache();
            InvalidateMarketValueCache();
        }

        private void InvalidateGraphicCache()
        {
            cachedGraphicEmpty = null;
            cachedGraphicFull = null;
            if (Spawned)
            {
                DirtyMapMesh(Map);
            }
        }

        public void NotifyContentsChanged()
        {
            InvalidateGraphicCache();
            InvalidateMarketValueCache();
        }

        public void Notify_HauledTo(Pawn hauler, Thing thing, int count)
        {
            InvalidateGraphicCache();
            InvalidateMarketValueCache();
        }

        public void InvalidateMarketValueCache()
        {
            cachedMarketValue = -1f;
        }

        public override string GetInspectString()
        {
            StringBuilder sb = new();

            Corpse corpse = ContainedCorpse;
            if (corpse != null)
            {
                Pawn innerPawn = corpse.InnerPawn;
                sb.AppendLine("USAC_CorpseBag_Contains".Translate(innerPawn.LabelShort));

                float value = CalculateCorpseValue(corpse);
                sb.Append("USAC_CorpseBag_Value".Translate(value.ToStringMoney()));
            }
            else
            {
                sb.Append("USAC_CorpseBag_Empty".Translate());
            }

            return sb.ToString();
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            if (StorageTabVisible)
            {
                foreach (var item in StorageSettingsClipboard.CopyPasteGizmosFor(storageSettings))
                {
                    yield return item;
                }
            }

            if (HasCorpse)
            {
                yield return new Command_Action
                {
                    defaultLabel = "USAC_CorpseBag_Eject".Translate(),
                    defaultDesc = "USAC_CorpseBag_EjectDesc".Translate(),
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/PodEject", false),
                    action = delegate
                    {
                        EjectContents();
                    }
                };
            }
        }

        #endregion

        #region 存档序列化

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref storageSettings, "storageSettings", this);
        }

        #endregion
    }
}
