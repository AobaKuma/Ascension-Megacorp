using System;
using System.Collections.Generic;
using UnityEngine;
using RimWorld;
using Verse;
using USAC.InternalUI;
using static USAC.InternalUI.PortalUIUtility;

namespace USAC
{
    // USAC门户界面
    public class Dialog_USACPortal : Window
    {
        #region 字段
        public string currentUrl { get; private set; } = "usac://internal/home";
        private string addressUrl = "usac://internal/home";
        private readonly Dictionary<string, IPortalPage> pageCache = new();
        private readonly Stack<string> history = new();

        // 动画控制器
        public readonly PortalAnimator Animator = new();

        private GUIStyle tacticalScrollbar;
        private GUIStyle tacticalScrollThumb;
        // URL参数缓存
        private string _lastCurrentUrl;
        private readonly Dictionary<string, string> _currentParamCache = new();
        private string _activeAnimUrl;
        private readonly Dictionary<string, string> _activeAnimParamCache = new();
        #endregion

        public override Vector2 InitialSize => new(1150f, 850f);
        protected override float Margin => 0f;

        public Dialog_USACPortal()
        {
            doCloseX = false;
            forcePause = true;
            absorbInputAroundWindow = true;
            doWindowBackground = false;
            drawShadow = false;

            RegisterPages();
        }

        private void RegisterPages()
        {
            pageCache["usac://internal/home"] = new Page_Home();
            pageCache["usac://internal/assets"] = new Page_Assets();
            pageCache["usac://internal/products"] = new Page_Products();
            pageCache["usac://internal/legal"] = new Page_Legal();
            pageCache["usac://internal/product"] = new Page_ProductDetail();
            pageCache["usac://internal/services"] = new Page_Services();
        }

        #region 核心绘制
        public override void DoWindowContents(Rect inRect)
        {
            // 绘制全局背景
            Rect fullRect = new(0, 0, InitialSize.x, InitialSize.y);
            Widgets.DrawBoxSolid(fullRect, ColWindowBg);
            DrawBackgroundGrid(fullRect);
            EnsureTacticalStyles();

            GUI.BeginGroup(inRect);
            // 绘制头部
            DrawBrowserHeader(new Rect(0, 0, inRect.width, 70));
            // 绘制主区域
            DrawMainFrame(new Rect(0, 70, inRect.width, inRect.height - 105));
            // 绘制状态栏
            DrawStatusBar(new Rect(0, inRect.height - 35, inRect.width, 35));
            GUI.EndGroup();
        }

        private void DrawMainFrame(Rect rect)
        {
            // 侧边导航栏
            Rect sideRect = new(rect.x, rect.y, 240, rect.height);
            Widgets.DrawBoxSolid(sideRect, new Color(0, 0, 0, 0.2f));

            float curY = rect.y + 40;
            DrawBookmarkItem(ref curY, 240, "USAC.UI.Nav.Home".Translate(), "usac://internal/home");
            DrawBookmarkItem(ref curY, 240, "USAC.UI.Nav.Services".Translate(), "usac://internal/services");
            DrawBookmarkItem(ref curY, 240, "USAC.UI.Nav.Products".Translate(), "usac://internal/products");
            DrawBookmarkItem(ref curY, 240, "USAC.UI.Nav.Assets".Translate(), "usac://internal/assets");
            DrawBookmarkItem(ref curY, 240, "USAC.UI.Nav.Legal".Translate(), "usac://internal/legal");

            // 内容视口
            Rect contentRect = new(rect.x + 240, rect.y, rect.width - 240, rect.height);
            GUI.BeginGroup(contentRect);
            Rect localContent = new(0, 0, contentRect.width, contentRect.height);

            // 注入滚动条样式
            var origBar = GUI.skin.verticalScrollbar;
            var origThumb = GUI.skin.verticalScrollbarThumb;
            GUI.skin.verticalScrollbar = tacticalScrollbar;
            GUI.skin.verticalScrollbarThumb = tacticalScrollThumb;

            DrawContent(localContent);

            GUI.skin.verticalScrollbar = origBar;
            GUI.skin.verticalScrollbarThumb = origThumb;

            GUI.EndGroup();
        }

        private void DrawContent(Rect localContent)
        {
            string baseUrl = GetBase(currentUrl);
            var anim = Animator;

            if (anim.IsPlaying)
            {
                string fromBase = GetBase(anim.FromUrl);
                string toBase = GetBase(anim.ToUrl);

                bool hasFrom = pageCache.TryGetValue(fromBase, out var fromPage);
                bool hasTo = pageCache.TryGetValue(toBase, out var toPage);

                if (!hasFrom || !hasTo)
                {
                    // 降级绘制
                    DrawSinglePage(baseUrl, localContent);
                    return;
                }

                switch (anim.Kind)
                {
                    case PortalAnimator.TransitionKind.CrossFade:
                        DrawCrossFade(localContent, fromPage, toPage, anim);
                        break;
                    case PortalAnimator.TransitionKind.SharedElement:
                        DrawSharedElement(localContent, fromPage, toPage, anim);
                        break;
                }
            }
            else
            {
                DrawSinglePage(baseUrl, localContent);
            }
        }

        // 普通十字渐隐绘制
        private void DrawCrossFade(Rect rect, IPortalPage from, IPortalPage to, PortalAnimator anim)
        {
            GUI.color = new Color(1, 1, 1, anim.GetFromAlpha());
            from.Draw(rect.ContractedBy(40), this);

            GUI.color = new Color(1, 1, 1, anim.GetToAlpha());
            to.Draw(rect.ContractedBy(40), this);

            GUI.color = Color.white;
        }

        // 卡片共享元素过渡绘制
        private void DrawSharedElement(Rect rect, IPortalPage from, IPortalPage to, PortalAnimator anim)
        {
            bool tempEnabled = GUI.enabled;

            if (!anim.IsBack)
            {
                // 进入详情禁用底层
                GUI.enabled = false;
                from.Draw(rect.ContractedBy(40), this);
                GUI.enabled = tempEnabled;
                to.Draw(rect.ContractedBy(40), this);
            }
            else
            {
                // 返回列表禁用详情
                GUI.enabled = false;
                to.Draw(rect.ContractedBy(40), this);
                GUI.enabled = tempEnabled;
                from.Draw(rect.ContractedBy(40), this);
            }
        }

        private void DrawSinglePage(string baseUrl, Rect rect)
        {
            if (pageCache.TryGetValue(baseUrl, out var page))
                page.Draw(rect.ContractedBy(40), this);
            else
                new Page_404().Draw(rect.ContractedBy(40), this);
        }
        #endregion

        #region 组件与导航
        private void DrawBrowserHeader(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, ColHeaderBg);
            DrawUIGradient(rect, new Color(1, 1, 1, 0.05f), new Color(0, 0, 0, 0.1f));

            float x = 20;
            if (DrawIconButton(new Rect(x, 20, 30, 30), "<", ColAccentCamo3)) NavigateBack();
            x += 40;
            if (DrawIconButton(new Rect(x, 20, 30, 30), "⌂", ColAccentCamo1)) NavigateTo("usac://internal/home");

            x += 50;
            Rect addressRect = new(x, 18, rect.width - x - 80, 34);
            Widgets.DrawBoxSolidWithOutline(addressRect, ColAddressBar, ColBorder);
            addressUrl = Widgets.TextField(new Rect(addressRect.x + 10, addressRect.y + 2, addressRect.width - 20, 30), addressUrl);

            if (DrawIconButton(new Rect(rect.width - 50, 18, 34, 34), "X", ColAccentRed)) Close();
        }

        private void DrawBookmarkItem(ref float y, float width, string label, string url)
        {
            Rect r = new(20, y, width - 40, 50);
            bool active = currentUrl == url || currentUrl.StartsWith(url + "?");

            if (active)
            {
                Widgets.DrawBoxSolid(r, new Color(1, 1, 1, 0.05f));
            }

            if (DrawTacticalButton(r, label, true, GameFont.Small))
            {
                NavigateTo(url);
            }
            y += 60;
        }

        private bool DrawIconButton(Rect r, string label, Color color)
        {
            return DrawTacticalButton(r, label, true, GameFont.Small);
        }
        #endregion

        #region 导航系统
        // 标准无过场跳转
        public void NavigateTo(string url)
        {
            if (currentUrl == url) return;

            // 强行重置动画状态
            Animator.Complete();

            history.Push(currentUrl);
            currentUrl = url;
            addressUrl = url;
        }

        // 渐变跳转
        public void NavigateWithFade(string url, float duration = 0.25f)
        {
            if (currentUrl == url) return;

            string prev = currentUrl;
            history.Push(prev);
            currentUrl = url;
            addressUrl = url;

            Animator.StartCrossFade(prev, url, false, duration);
        }

        // 共享元素跳转
        public void NavigateToWithSharedElement(string url, Rect cardScreenRect, float duration = 0.3f)
        {
            if (currentUrl == url) return;

            string prev = currentUrl;
            history.Push(prev);
            currentUrl = url;
            addressUrl = url;

            Animator.StartSharedElement(prev, url, cardScreenRect, duration);
        }

        public void NavigateBack()
        {
            if (history.Count == 0 || Animator.IsPlaying) return;

            string prev = currentUrl;
            string target = history.Pop();

            // 从商品详情返回执行动效
            if (GetBase(prev) == "usac://internal/product")
            {
                // 切换前捕获物理锚点
                float sx = float.Parse(GetParamFrom(prev, "sx") ?? "0");
                float sy = float.Parse(GetParamFrom(prev, "sy") ?? "0");
                float sw = float.Parse(GetParamFrom(prev, "sw") ?? "0");
                float sh = float.Parse(GetParamFrom(prev, "sh") ?? "0");
                Rect anchorRect = new(sx, sy, sw, sh);

                // 计算线性速度时长
                float dist = Vector2.Distance(new Vector2(sx + sw / 2f, sy + sh / 2f), new Vector2(210f, 280f));
                float dur = Mathf.Clamp(dist / 2500f, 0.15f, 0.45f);

                currentUrl = target;
                addressUrl = currentUrl;
                Animator.StartSharedElementBack(prev, currentUrl, anchorRect, dur);
            }
            else
            {
                currentUrl = target;
                addressUrl = target;
                Animator.Complete();
            }
        }

        public string GetParamActive(string key)
        {
            if (Animator.IsPlaying)
            {
                string sourceUrl = null;
                if (GetBase(Animator.FromUrl) == "usac://internal/product")
                    sourceUrl = Animator.FromUrl;
                else if (GetBase(Animator.ToUrl) == "usac://internal/product")
                    sourceUrl = Animator.ToUrl;

                if (sourceUrl != null)
                {
                    if (sourceUrl != _activeAnimUrl)
                    {
                        _activeAnimParamCache.Clear();
                        _activeAnimUrl = sourceUrl;
                        ParseUrlParams(sourceUrl, _activeAnimParamCache);
                    }
                    return _activeAnimParamCache.TryGetValue(key, out var v) ? v : null;
                }
            }
            return GetParam(key);
        }

        // 获取路径基准
        public static string GetBase(string url)
        {
            if (url == null) return null;
            int idx = url.IndexOf('?');
            return idx < 0 ? url : url.Substring(0, idx);
        }

        // 获取当前参数
        public string GetParam(string key)
        {
            if (currentUrl != _lastCurrentUrl)
            {
                _currentParamCache.Clear();
                _lastCurrentUrl = currentUrl;
                ParseUrlParams(currentUrl, _currentParamCache);
            }
            return _currentParamCache.TryGetValue(key, out var val) ? val : null;
        }

        // 从指定 URL 中获取参数
        public static string GetParamFrom(string url, string key)
        {
            if (url == null || !url.Contains('?')) return null;
            string query = url.Substring(url.IndexOf('?') + 1);
            foreach (string pair in query.Split('&'))
            {
                int eq = pair.IndexOf('=');
                if (eq > 0 && pair.Substring(0, eq) == key) return pair.Substring(eq + 1);
            }
            return null;
        }

        // 解析 URL 查询参数到字典
        private static void ParseUrlParams(string url, Dictionary<string, string> dict)
        {
            if (url == null || !url.Contains('?')) return;
            string query = url.Substring(url.IndexOf('?') + 1);
            foreach (string pair in query.Split('&'))
            {
                int eq = pair.IndexOf('=');
                if (eq > 0) dict[pair.Substring(0, eq)] = pair.Substring(eq + 1);
            }
        }
        #endregion

        #region 辅助系统
        // 初始化滚动条样式
        private void EnsureTacticalStyles()
        {
            if (tacticalScrollbar != null) return;
            tacticalScrollbar = new GUIStyle(GUI.skin.verticalScrollbar) { fixedWidth = 4f };
            tacticalScrollThumb = new GUIStyle(GUI.skin.verticalScrollbarThumb) { fixedWidth = 4f };
            tacticalScrollThumb.normal.background = SolidColorMaterials.NewSolidColorTexture(ColAccentCamo3);
        }

        private static Texture2D cachedLogo;
        private static Texture2D Logo => cachedLogo ??= ContentFinder<Texture2D>.Get("UI/StyleCategories/USACIcon");

        private static Texture2D cachedGridTex;
        private static int cachedGridW, cachedGridH;

        // 获取或创建网格纹理
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

            if (Logo != null)
            {
                float logoSize = 460f;
                Vector2 center = new(240 + (rect.width - 240) / 2f, rect.height / 2f);
                GUI.color = new Color(1, 1, 1, 0.15f);
                GUI.DrawTexture(new Rect(center.x - logoSize / 2f, center.y - logoSize / 2f, logoSize, logoSize), Logo);
                GUI.color = Color.white;
            }
        }

        private void DrawStatusBar(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, ColHeaderBg);
            GUI.color = ColAccentCamo3;
            Widgets.DrawLineHorizontal(0, rect.y, rect.width);
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(20, rect.y + 9, 400, 20), "USAC.UI.StatusBar".Translate());
            GUI.color = Color.white;
        }
        #endregion
    }
}
