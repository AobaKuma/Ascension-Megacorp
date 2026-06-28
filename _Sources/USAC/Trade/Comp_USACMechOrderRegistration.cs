using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace USAC
{
    public class CompProperties_USACMechOrderRegistration : CompProperties
    {
        public CompProperties_USACMechOrderRegistration()
        {
            compClass = typeof(Comp_USACMechOrderRegistration);
        }
    }

    // 机兵订单注册组件
    public class Comp_USACMechOrderRegistration : ThingComp
    {
        public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
        {
            if (selPawn?.Map == null) yield break;

            var ext = parent.def.GetModExtension<ModExtension_MechOrder>();
            if (ext?.mechKindDef == null) yield break;

            Map map = selPawn.Map;
            int fee = Mathf.CeilToInt(parent.def.GetStatValueAbstract(StatDefOf.MarketValue) * 0.2f);
            int available = DebtBondOperations.GetBondCountOnMap(map);

            string label = "USAC_MechOrder_Register".Translate(parent.LabelCap, fee);

            if (available < fee)
            {
                yield return new FloatMenuOption(label + " (" + "USAC_MechOrder_NoFunds".Translate(available, fee) + ")", null);
                yield break;
            }

            yield return new FloatMenuOption(label, () => Execute(selPawn, ext.mechKindDef, fee));
        }

        private void Execute(Pawn pawn, PawnKindDef mechKind, int fee)
        {
            Map map = pawn.Map;
            DebtBondOperations.ConsumeBonds(map, fee);
            parent.Destroy(DestroyMode.Vanish);
            USAC_MechTradeUtility.DropMech(mechKind, pawn);
            USACDeliveryManager.Instance?.RequestStartPlacement();
            Messages.Message("USAC_MechOrder_Registered".Translate(mechKind.label.CapitalizeFirst(), fee), MessageTypeDefOf.PositiveEvent);
        }
    }
}
