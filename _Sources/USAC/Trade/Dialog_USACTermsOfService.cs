using UnityEngine;
using Verse;
using RimWorld;
using static USAC.InternalUI.PortalUIUtility;

namespace USAC
{
    // USAC用户协议对话框
    [StaticConstructorOnStartup]
    public class Dialog_USACTermsOfService : Window
    {
        #region 字段

        private readonly bool giftsOnly;
        private readonly Window origDialog;
        private Vector2 scrollPosition;
        private bool hasScrolledToBottom;

        #endregion

        #region 属性

        public override Vector2 InitialSize => new(800f, 600f);
        protected override float Margin => 0f;

        #endregion

        #region 构造函数

        public Dialog_USACTermsOfService(bool giftsOnly = false, Window origDialog = null)
        {
            this.giftsOnly = giftsOnly;
            this.origDialog = origDialog;
            doCloseX = false;
            forcePause = true;
            absorbInputAroundWindow = true;
            doWindowBackground = false;
            drawShadow = false;
        }

        #endregion

        #region 核心绘制

        public override void DoWindowContents(Rect inRect)
        {
            Rect fullRect = new(0, 0, InitialSize.x, InitialSize.y);
            Widgets.DrawBoxSolid(fullRect, ColWindowBg);
            DrawBackgroundGrid(fullRect);

            GUI.BeginGroup(inRect);

            DrawHeader(new Rect(0, 0, inRect.width, 70));
            
            BeginTacticalScroll(out var prevBar, out var prevThumb, out var prevColor);
            DrawTermsContent(new Rect(0, 80, inRect.width, inRect.height - 160));
            EndTacticalScroll(prevBar, prevThumb, prevColor);

            DrawFooter(new Rect(0, inRect.height - 70, inRect.width, 70));

            GUI.EndGroup();
        }

        private void DrawHeader(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, ColHeaderBg);
            DrawUIGradient(rect, new Color(1, 1, 1, 0.05f), new Color(0, 0, 0, 0.1f));

            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = ColAccentCamo1;
            Widgets.Label(rect, "USAC.Terms.Title".Translate());
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawTermsContent(Rect rect)
        {
            Widgets.DrawBoxSolidWithOutline(rect, new Color(0, 0, 0, 0.2f), ColBorder);

            Rect innerRect = rect.ContractedBy(10);
            
            Text.Font = GameFont.Small;
            string termsText = "USAC.Terms.Content".Translate();
            float textHeight = Text.CalcHeight(termsText, innerRect.width - 24f); 
            
            Rect viewRect = new(0, 0, innerRect.width - 24f, textHeight);

            Widgets.BeginScrollView(innerRect, ref scrollPosition, viewRect);
            
            GUI.color = new Color(0.9f, 0.9f, 0.9f);
            Widgets.Label(new Rect(0, 0, viewRect.width, textHeight), termsText);
            GUI.color = Color.white;
            
            if (!hasScrolledToBottom)
            {
                float verticalScrollRange = viewRect.height - innerRect.height;
                if (verticalScrollRange <= 0f || scrollPosition.y >= verticalScrollRange - 1f)
                {
                    hasScrolledToBottom = true;
                }
            }
            
            Widgets.EndScrollView();
        }

        private void DrawFooter(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, ColHeaderBg);
            GUI.color = ColAccentCamo3;
            Widgets.DrawLineHorizontal(0, rect.y, rect.width);
            GUI.color = Color.white;

            float buttonWidth = 180f;
            float buttonHeight = 40f;
            float centerY = rect.y + (rect.height - buttonHeight) / 2f;
            float centerX = rect.width / 2f;

            if (DrawTacticalButton(
                new Rect(centerX - buttonWidth - 10, centerY, buttonWidth, buttonHeight),
                "USAC.Terms.Decline".Translate(), true, GameFont.Small, "decline_terms"))
            {
                OnDecline();
            }

            if (DrawTacticalButton(
                new Rect(centerX + 10, centerY, buttonWidth, buttonHeight),
                "USAC.Terms.Accept".Translate(), hasScrolledToBottom, GameFont.Small, "accept_terms"))
            {
                OnAccept();
            }
        }

        #endregion

        #region 事件处理

        private void OnAccept()
        {
            // 标记全局已处理并已接受
            if (USAC_Mod.Settings != null)
            {
                USAC_Mod.Settings.termsProcessed = true;
                USAC_Mod.Settings.hasAcceptedTerms = true;
                LoadedModManager.GetMod<USAC_Mod>().GetSettings<USAC_ModSettings>().Write();
            }

            Close();
            
            var usacTerminal = new Dialog_USACTerminal(giftsOnly);
            Find.WindowStack.Add(usacTerminal);
        }

        private void OnDecline()
        {
            Close();
            Messages.Message("USAC.Terms.DeclineMessage".Translate(), MessageTypeDefOf.NeutralEvent);

            // 标记全局已处理但未接受
            if (USAC_Mod.Settings != null)
            {
                USAC_Mod.Settings.termsProcessed = true;
                USAC_Mod.Settings.hasAcceptedTerms = false;
                LoadedModManager.GetMod<USAC_Mod>().GetSettings<USAC_ModSettings>().Write();
            }
            
            if (origDialog != null && TradeSession.Active)
            {
                Find.WindowStack.Add(origDialog);
            }
        }

        #endregion

        #region 辅助方法

        private static Texture2D cachedGridTex;
        private static int cachedGridW, cachedGridH;

        private static Texture2D GetOrCreateGridTex(int w, int h)
        {
            if (cachedGridTex != null && cachedGridW == w && cachedGridH == h) return cachedGridTex;
            if (cachedGridTex != null) UnityEngine.Object.Destroy(cachedGridTex);
            cachedGridTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color gridCol = new(1f, 1f, 1f, 0.03f);
            Color[] pixels = new Color[w * h];
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    pixels[y * w + x] = (x % 50 == 0 || y % 50 == 0) ? gridCol : Color.clear;
            cachedGridTex.SetPixels(pixels);
            cachedGridTex.Apply();
            cachedGridW = w; cachedGridH = h;
            return cachedGridTex;
        }

        private void DrawBackgroundGrid(Rect rect)
        {
            GUI.DrawTexture(rect, GetOrCreateGridTex((int)rect.width, (int)rect.height));

            var logo = ContentFinder<Texture2D>.Get("UI/StyleCategories/USACIcon", false);
            if (logo != null)
            {
                float logoSize = 300f;
                Vector2 center = new(rect.width / 2f, rect.height / 2f);
                GUI.color = new Color(1, 1, 1, 0.1f);
                GUI.DrawTexture(new Rect(center.x - logoSize / 2f, center.y - logoSize / 2f, logoSize, logoSize), logo);
                GUI.color = Color.white;
            }
        }

        public override bool CausesMessageBackground()
        {
            return true;
        }

        #endregion
    }
}
