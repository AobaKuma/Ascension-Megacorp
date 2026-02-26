using System;
using RimWorld.QuestGen;
using Verse;

namespace USAC
{
    public class QuestNode_USAC_CalculateBondCount : QuestNode
    {
        public SlateRef<double> rewardValue;

        [NoTranslate]
        public SlateRef<string> storeAs;

        protected override bool TestRunInt(Slate slate)
        {
            return !storeAs.GetValue(slate).NullOrEmpty();
        }

        protected override void RunInt()
        {
            double value = rewardValue.GetValue(QuestGen.slate);
            int bondCount = Math.Max(1, (int)Math.Floor(value / 1000.0));
            QuestGen.slate.Set(storeAs.GetValue(QuestGen.slate), bondCount);
        }
    }
}
