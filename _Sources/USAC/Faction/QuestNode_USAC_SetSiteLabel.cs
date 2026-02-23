using RimWorld.Planet;
using RimWorld.QuestGen;
using Verse;

namespace USAC
{
    // 设置站点自定义标签
    public class QuestNode_USAC_SetSiteLabel : QuestNode
    {
        public SlateRef<Site> site;
        public SlateRef<string> label;

        protected override bool TestRunInt(Slate slate)
        {
            return true;
        }

        protected override void RunInt()
        {
            var s = site.GetValue(QuestGen.slate);
            var l = label.GetValue(QuestGen.slate);
            if (s != null && !l.NullOrEmpty())
                s.customLabel = l;
        }
    }
}
