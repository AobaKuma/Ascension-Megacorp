using UnityEngine;
using RimWorld;
using Verse;

namespace USAC
{
    public class USAC_ModSettings : ModSettings
    {
        // 全局启用终端
        public bool enableUSACTerminal = true;
        // 协议处理状态
        public bool termsProcessed = false;
        // 协议接受状态
        public bool hasAcceptedTerms = false;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref enableUSACTerminal, "enableUSACTerminal", true);
            Scribe_Values.Look(ref termsProcessed, "termsProcessed", false);
            Scribe_Values.Look(ref hasAcceptedTerms, "hasAcceptedTerms", false);
        }
    }

    public class USAC_Mod : Mod
    {
        public static USAC_ModSettings Settings;

        public USAC_Mod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<USAC_ModSettings>();
        }

        public override string SettingsCategory()
        {
            return "USAC.Settings.CategoryName".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            if (listing.ButtonText("USAC.Settings.ResetAgreement".Translate()))
            {
                Settings.termsProcessed = false;
                Messages.Message("USAC.Settings.ResetAgreementSuccess".Translate(), MessageTypeDefOf.TaskCompletion, false);
            }
            // 说明文字建议换行显示
            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;
            listing.Label("USAC.Settings.ResetAgreementDesc".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            listing.End();
            base.DoSettingsWindowContents(inRect);
        }
    }
}
