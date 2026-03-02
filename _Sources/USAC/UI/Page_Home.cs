using UnityEngine;
using Verse;
using static USAC.InternalUI.PortalUIUtility;

namespace USAC.InternalUI
{
    // USAC门户首页
    public class Page_Home : IPortalPage
    {
        public string Title => "USAC.UI.Home.Title".Translate();
        private Vector2 scrollPos;

        // 缓存主图缩略纹理
        private static Texture2D _heroBanner;
        private static Texture2D HeroBanner => _heroBanner ??= ContentFinder<Texture2D>.Get("UI/USAC/HeroBanner", false);

        public void Draw(Rect rect, Dialog_USACPortal parent)
        {
            Widgets.BeginScrollView(rect, ref scrollPos, new Rect(0, 0, rect.width - 20, 850));
            float y = 0;
            float w = rect.width - 20;

            // Hero Banner
            Rect banner = new(0, y, w, 280);
            DrawUIGradient(banner, ColHeaderBg, ColWindowBg);
            Widgets.DrawBoxSolidWithOutline(banner, Color.clear, ColBorder);

            // 绘制集团形象图
            if (HeroBanner != null)
            {
                float imgAspect = (float)HeroBanner.width / HeroBanner.height;
                float imgH = banner.height * 0.75f;
                float imgW = imgH * imgAspect;
                Rect imgRect = new(banner.center.x - imgW / 2f, banner.y + (banner.height - imgH) / 2f, imgW, imgH);
                GUI.color = new Color(1f, 1f, 1f, 0.55f);
                GUI.DrawTexture(imgRect, HeroBanner, ScaleMode.ScaleToFit);
                GUI.color = Color.white;
            }

            Rect bannerContent = banner.ContractedBy(30);
            DrawColoredLabel(bannerContent.TopPartPixels(35), "UNITED STELLAR ARMAMENT COMPANY", ColAccentCamo1, GameFont.Medium);
            DrawColoredLabel(new Rect(bannerContent.x, bannerContent.y + 45, bannerContent.width, 100), "USAC.UI.Home.Banner".Translate(), Color.white, GameFont.Medium);
            y += 280;

            // Slogan文字条
            Rect sloganRect = new(0, y, w, 80);
            Text.Anchor = TextAnchor.MiddleCenter;
            DrawColoredLabel(sloganRect, "USAC.UI.Home.Slogan".Translate(), ColAccentCamo2, GameFont.Small, TextAnchor.MiddleCenter);
            Text.Anchor = TextAnchor.UpperLeft;
            y += 80;

            // Bento功能矩阵
            float gridY = y + 10;
            float gap = 15f;
            float margin = 12f;
            float ew = w - margin * 2;

            // Row1企业服务与资产
            float r1H = 220f;
            DrawBentoTile(new Rect(margin, gridY, ew * 0.63f, r1H), "USAC.UI.Home.Bento.Services.Title".Translate(), "USAC.UI.Home.Bento.Services.Desc".Translate(), "usac://internal/services", parent);
            DrawBentoTile(new Rect(margin + ew * 0.63f + gap, gridY, ew * 0.37f - gap, r1H), "USAC.UI.Home.Bento.Assets.Title".Translate(), "USAC.UI.Home.Bento.Assets.Desc".Translate(), "usac://internal/assets", parent);
            gridY += r1H + gap;

            // Row2法律与机兵产品
            float r2H = 180f;
            DrawBentoTile(new Rect(margin, gridY, ew * 0.37f, r2H), "USAC.UI.Home.Bento.Legal.Title".Translate(), "USAC.UI.Home.Bento.Legal.Desc".Translate(), "usac://internal/legal", parent);
            DrawBentoTile(new Rect(margin + ew * 0.37f + gap, gridY, ew * 0.63f - gap, r2H), "USAC.UI.Home.Bento.Products.Title".Translate(), "USAC.UI.Home.Bento.Products.Desc".Translate(), "usac://internal/products", parent);

            Widgets.EndScrollView();
        }

        private void DrawBentoTile(Rect r, string title, string desc, string url, Dialog_USACPortal parent)
        {
            bool clickable = !url.NullOrEmpty();
            if (DrawBentoBox(r, (tileRect) =>
            {
                Rect inner = tileRect.ContractedBy(20);
                DrawColoredLabel(inner.TopPartPixels(30), title.ToUpper(), ColAccentCamo1, GameFont.Small);
                DrawColoredLabel(new Rect(inner.x, inner.y + 35, inner.width, 80), desc, ColTextActive, GameFont.Tiny);
            }, clickable, url))
            {
                parent.NavigateTo(url);
            }
        }
    }
}
