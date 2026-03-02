using UnityEngine;
using Verse;
using System.Collections.Generic;
using System.Globalization;
using static USAC.InternalUI.PortalUIUtility;

namespace USAC.InternalUI
{
    // USAC 产品目录页面
    public class Page_Products : IPortalPage
    {
        public string Title => "USAC.UI.Products.Title".Translate();
        private Vector2 scrollPos;
        private string activeCategory = "MECH";

        #region 缓存
        private List<USACProductDef> cachedProducts;
        private string lastCategory;

        private void UpdateCache()
        {
            if (cachedProducts != null && lastCategory == activeCategory) return;
            cachedProducts = DefDatabase<USACProductDef>.AllDefsListForReading
                .FindAll(p => p.category == activeCategory);
            lastCategory = activeCategory;
        }
        #endregion

        public void Draw(Rect rect, Dialog_USACPortal parent)
        {
            float y = 5;
            DrawCategoryTabs(ref y, rect.width);
            UpdateCache();

            // 产品列表参数
            const float rowHeight = 180f;
            const float gap = 20f;
            const float paddingX = 12f;
            float viewHeight = Mathf.Max(rect.height, (Mathf.CeilToInt(cachedProducts.Count / 2f) * rowHeight) + 50);
            Rect viewRect = new(0, 0, rect.width - 20, viewHeight);
            Rect scrollRect = new(0, y, rect.width, rect.height - y);

            Widgets.BeginScrollView(scrollRect, ref scrollPos, viewRect);

            // 虚拟化可见行范围
            int firstVisibleRow = Mathf.FloorToInt(scrollPos.y / rowHeight);
            int lastVisibleRow = Mathf.CeilToInt((scrollPos.y + scrollRect.height) / rowHeight);
            int startIdx = Mathf.Max(0, firstVisibleRow * 2);
            int endIdx = Mathf.Min(cachedProducts.Count, (lastVisibleRow + 1) * 2);

            float cardW = (rect.width - 20 - paddingX * 2 - gap) / 2;
            for (int i = startIdx; i < endIdx; i++)
            {
                float rowY = (i / 2) * rowHeight;
                float x = (i % 2 == 0) ? paddingX : paddingX + cardW + gap;
                DrawCard(rowY, x, cardW, cachedProducts[i], parent, scrollRect);
            }

            Widgets.EndScrollView();
        }

        private void DrawCard(float y, float x, float w, USACProductDef product, Dialog_USACPortal parent, Rect scrollRect)
        {
            var anim = parent.Animator;
            string key = product.defName;

            // 关联共享元素过渡
            bool isSharedTrans = anim.IsPlaying && anim.Kind == PortalAnimator.TransitionKind.SharedElement;
            string detailUrl = isSharedTrans ? (anim.IsBack ? anim.FromUrl : anim.ToUrl) : null;
            string animatingDef = detailUrl != null ? Dialog_USACPortal.GetParamFrom(detailUrl, "def") : null;

            float visualAlpha = 1f;
            float offsetX = 0f;

            if (isSharedTrans && animatingDef != null)
            {
                if (animatingDef == product.defName) return; // 主角由详情页绘制

                float t = anim.IsBack ? (1f - anim.CurvedProgress) : anim.CurvedProgress;
                visualAlpha = 1f - t;
                offsetX = ((x < 100f) ? -150f : 150f) * t;
            }

            if (visualAlpha <= 0.01f) return;

            GUI.color = new Color(1, 1, 1, visualAlpha);
            Rect r = new(x + offsetX, y, w, 160);

            if (DrawBentoBox(r, (cardRect) =>
            {
                Rect inner = cardRect.ContractedBy(15);
                Rect iconRect = new(inner.x, inner.y, 100, 130);
                Rect textRect = new(inner.x + 110, inner.y, inner.width - 110, inner.height);

                DrawProductPreview(iconRect, product, visualAlpha);

                GUI.color = ColAccentCamo1.ToTransp(visualAlpha);
                Widgets.Label(textRect.TopPartPixels(25), product.label);
                GUI.color = ColTextMuted.ToTransp(visualAlpha);
                Widgets.Label(new Rect(textRect.x, textRect.y + 25, textRect.width, 20), product.subLabel);
                GUI.color = ColTextActive.ToTransp(visualAlpha);
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(textRect.x, textRect.y + 50, textRect.width, 80), product.CachedDescription);
                GUI.color = Color.white;
            }, true, key))
            {
                // 更新返回目标坐标
                float visY = y - scrollPos.y + scrollRect.y;
                if (isSharedTrans && animatingDef == product.defName && anim.IsBack)
                {
                    anim.SharedElementTarget = new Rect(x, visY, w, 160f);
                }

                if (!anim.IsPlaying)
                {
                    float dist = Vector2.Distance(new Vector2(x + w / 2f, visY + 80f), new Vector2(210f, 280f));
                    float dur = Mathf.Clamp(dist / 2500f, 0.12f, 0.4f);
                    Rect cardScreenRect = new(x, visY, w, 160f);
                    string url = $"usac://internal/product?def={product.defName}&sx={x.ToString(CultureInfo.InvariantCulture)}&sy={visY.ToString(CultureInfo.InvariantCulture)}&sw={w.ToString(CultureInfo.InvariantCulture)}&sh=160";
                    parent.NavigateToWithSharedElement(url, cardScreenRect, dur);
                }
            }
            GUI.color = Color.white;
        }

        private void DrawCategoryTabs(ref float y, float width)
        {
            float tabW = 100;
            DrawTab(new Rect(0, y, tabW, 35), "MECH", "USAC.UI.Products.Tab.Mech".Translate());
            DrawTab(new Rect(tabW + 10, y, tabW, 35), "WEAPON", "USAC.UI.Products.Tab.Weapon".Translate());
            DrawTab(new Rect((tabW + 10) * 2, y, tabW, 35), "APPAREL", "USAC.UI.Products.Tab.Apparel".Translate());
            DrawTab(new Rect((tabW + 10) * 3, y, tabW, 35), "SUPPLY", "USAC.UI.Products.Tab.Supply".Translate());
            y += 50;
        }

        private void DrawTab(Rect r, string id, string label)
        {
            bool active = activeCategory == id;
            if (DrawTacticalButton(r, label, true, GameFont.Tiny, key: $"tab_{id}")) activeCategory = id;
            if (active)
            {
                // 激活态指示线
                Widgets.DrawLineHorizontal(r.x + 5, r.yMax - 2, r.width - 10);
            }
        }
    }
}
