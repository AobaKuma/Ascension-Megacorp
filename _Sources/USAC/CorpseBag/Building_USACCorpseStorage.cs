using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace USAC
{
    // USAC标准尸体储存方案
    public class Building_USACCorpseStorage : Building, IThingHolder, IHaulDestination
    {
        #region 字段
        private ThingOwner<Corpse> innerContainer;
        private CompPowerTrader powerComp;
        private StorageSettings storageSettings;
        #endregion

        #region 属性
        public int MaxCapacity => 16;
        public bool IsFull => innerContainer.Count >= MaxCapacity;
        public bool IsEmpty => innerContainer.Count == 0;
        public int CorpseCount => innerContainer.Count;
        
        public override float MarketValue
        {
            get
            {
                float total = def.BaseMarketValue;
                foreach (Corpse corpse in innerContainer)
                    total += Building_CorpseBag.CalculateCorpseValue(corpse);
                return total;
            }
        }

        public float CorpseOnlyValue
        {
            get
            {
                float total = 0f;
                foreach (Corpse corpse in innerContainer)
                    total += Building_CorpseBag.CalculateCorpseValue(corpse);
                return total;
            }
        }
        #endregion

        #region 生命周期
        public Building_USACCorpseStorage()
        {
            innerContainer = new ThingOwner<Corpse>(this, false);
        }

        public override void PostMake()
        {
            base.PostMake();
            storageSettings = new StorageSettings(this);
            if (def.building.defaultStorageSettings != null)
                storageSettings.CopyFrom(def.building.defaultStorageSettings);
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            powerComp = GetComp<CompPowerTrader>();
        }

        public override void TickRare()
        {
            base.TickRare();
            
            if (powerComp?.PowerOn == true && innerContainer.Count > 0)
            {
                foreach (Corpse corpse in innerContainer)
                {
                    CompRottable rot = corpse.TryGetComp<CompRottable>();
                    if (rot != null)
                        rot.RotProgress = 0f;
                }
            }
        }
        #endregion

        #region IThingHolder实现
        public ThingOwner GetDirectlyHeldThings()
        {
            return innerContainer;
        }

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            outChildren.Clear();
        }
        #endregion

        #region IStoreSettingsParent实现
        public bool StorageTabVisible => true;

        public StorageSettings GetStoreSettings()
        {
            return storageSettings;
        }

        public StorageSettings GetParentStoreSettings()
        {
            return def.building?.fixedStorageSettings;
        }

        public void Notify_SettingsChanged()
        {
        }
        #endregion

        #region IHaulDestination实现
        public bool HaulDestinationEnabled => true;

        public bool Accepts(Thing thing)
        {
            if (innerContainer.Count >= MaxCapacity)
            {
                if (!(thing is Corpse corpse))
                    return false;
                if (!innerContainer.InnerListForReading.Contains(corpse))
                    return false;
            }

            if (thing is Corpse c)
            {
                if (c.GetRotStage() != RotStage.Fresh)
                    return false;

                if (c.InnerPawn?.RaceProps?.Humanlike != true)
                    return false;

                if (storageSettings != null && !storageSettings.AllowedToAccept(thing))
                    return false;

                return innerContainer.CanAcceptAnyOf(thing);
            }

            return false;
        }
        #endregion

        #region 存储逻辑
        public bool TryAcceptThing(Thing thing, bool allowSpecialEffects = true)
        {
            if (!Accepts(thing))
                return false;

            if (thing.holdingOwner != null)
                thing.holdingOwner.Remove(thing);

            if (innerContainer.TryAdd(thing))
            {
                if (thing.Spawned)
                    thing.DeSpawn();
                return true;
            }

            return false;
        }

        public int SpaceRemainingFor(ThingDef thingDef)
        {
            if (thingDef.IsCorpse)
                return Mathf.Max(0, MaxCapacity - innerContainer.Count);
            
            return 0;
        }

        public void EjectContents()
        {
            if (!Spawned || IsEmpty)
                return;

            innerContainer.TryDropAll(InteractionCell, Map, ThingPlaceMode.Near);
        }

        public IEnumerable<Corpse> GetStoredCorpses()
        {
            return innerContainer.InnerListForReading;
        }

        public void InvalidateMarketValueCache()
        {
        }
        #endregion

        #region UI显示
        public override string GetInspectString()
        {
            StringBuilder sb = new();
            
            sb.AppendLine("USAC.UI.Storage.Capacity".Translate(CorpseCount, MaxCapacity));
            
            if (powerComp != null)
            {
                string powerStatus = powerComp.PowerOn 
                    ? "USAC.UI.Storage.RefrigerationActive".Translate() 
                    : "USAC.UI.Storage.NoPower".Translate();
                sb.AppendLine(powerStatus);
            }

            if (!IsEmpty)
            {
                sb.Append("USAC_CorpseBag_Value".Translate(CorpseOnlyValue.ToStringMoney()));
            }

            return sb.ToString().TrimEnd();
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var g in base.GetGizmos())
                yield return g;

            foreach (var item in StorageSettingsClipboard.CopyPasteGizmosFor(storageSettings))
                yield return item;

            if (!IsEmpty)
            {
                yield return new Command_Action
                {
                    defaultLabel = "USAC_CorpseBag_Eject".Translate(),
                    defaultDesc = "USAC_CorpseBag_EjectDesc".Translate(),
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/PodEject"),
                    action = EjectContents
                };
            }
        }
        #endregion

        #region 序列化
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
            Scribe_Deep.Look(ref storageSettings, "storageSettings", this);
            
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (innerContainer == null)
                    innerContainer = new ThingOwner<Corpse>(this, false);
                if (storageSettings == null)
                {
                    storageSettings = new StorageSettings(this);
                    if (def.building.defaultStorageSettings != null)
                        storageSettings.CopyFrom(def.building.defaultStorageSettings);
                }
            }
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            if (mode == DestroyMode.KillFinalize)
            {
                innerContainer.ClearAndDestroyContents();
                
                if (Spawned)
                {
                    int steelAmount = Rand.RangeInclusive(3, 9);
                    Thing steel = ThingMaker.MakeThing(ThingDefOf.Steel);
                    steel.stackCount = steelAmount;
                    GenPlace.TryPlaceThing(steel, Position, Map, ThingPlaceMode.Near);
                }
            }
            else if (Spawned)
            {
                innerContainer.TryDropAll(Position, Map, ThingPlaceMode.Near);
            }
            
            base.Destroy(mode);
        }
        #endregion
    }
}
