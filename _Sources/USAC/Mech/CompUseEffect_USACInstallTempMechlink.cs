using Verse;
using RimWorld;

namespace USAC
{
    // 支持有效期堆叠的机控血清逻辑
    public class CompUseEffect_USACInstallTempMechlink : CompUseEffect_InstallImplantMechlink
    {
        public override void DoEffect(Pawn user)
        {
            Hediff existing = user.health.hediffSet.GetFirstHediffOfDef(Props.hediffDef);
            if (existing != null)
            {
                var disappears = existing.TryGetComp<HediffComp_Disappears>();
                if (disappears != null)
                {
                    disappears.ticksToDisappear += 1800000;
                    Messages.Message("USAC.Message.Renewed".Translate(user.LabelShort), user, MessageTypeDefOf.PositiveEvent);
                    return;
                }
            }
            base.DoEffect(user);
        }

        public override AcceptanceReport CanBeUsedBy(Pawn p)
        {
            if (p.health.hediffSet.HasHediff(Props.hediffDef))
            {
                return true;
            }
            return base.CanBeUsedBy(p);
        }
    }
}
