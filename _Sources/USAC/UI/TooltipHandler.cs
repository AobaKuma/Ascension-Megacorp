using UnityEngine;
using Verse;
using static USAC.InternalUI.PortalUIUtility;

namespace USAC
{
    // USAC统一Tooltip处理器
    [StaticConstructorOnStartup]
    public static class TooltipHandler
    {
        #region 字段
        private static Texture2D cachedLogo;
        private static Texture2D Logo => cachedLogo ??= ContentFinder<Texture2D>.Get("UI/StyleCategories/USACIcon", false);
        #endregion

        #region 公共方法
        // 绘制即时Tooltip 无延迟显示
        public static void DrawInstantTooltip(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            // 只在Repaint阶段绘制 与原版保持一致
            if (Event.current.type != EventType.Repaint) return;

            // 计算Tooltip尺寸
            Text.Font = GameFont.Small;
            float padding = 8f;
            float maxWidth = 400f;
            float width = Mathf.Min(Text.CalcSize(text).x + padding * 2, maxWidth);
            float height = Text.CalcHeight(text, width - padding * 2) + padding * 2;

            // 直接使用全局鼠标位置 不受GUI容器影响
            Vector2 mousePos = UI.MousePositionOnUIInverted;

            // 计算Tooltip位置 模拟GenUI.GetMouseAttachedWindowPos的逻辑
            float y = (mousePos.y + 14f + height < UI.screenHeight)
                ? (mousePos.y + 14f)
                : ((mousePos.y - 5f - height >= 0f)
                    ? (mousePos.y - 5f - height)
                    : (UI.screenHeight - (14f + height)));

            float x = (mousePos.x + 16f + width < UI.screenWidth)
                ? (mousePos.x + 16f)
                : (mousePos.x - 4f - width);

            Rect tooltipRect = new Rect(x, y, width, height);

            // 使用ImmediateWindow在Super层绘制 确保不被遮挡
            int windowId = text.GetHashCode();
            Find.WindowStack.ImmediateWindow(windowId, tooltipRect, WindowLayer.Super, delegate
            {
                Rect localRect = new Rect(0, 0, width, height);

                // 绘制背景
                Widgets.DrawBoxSolid(localRect, new Color(0.1f, 0.1f, 0.1f, 0.95f));
                GUI.color = new Color(1f, 1f, 1f, 0.3f);
                Widgets.DrawBox(localRect, 1);
                GUI.color = Color.white;

                // 绘制文本
                Rect textRect = localRect.ContractedBy(padding);
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                Widgets.Label(textRect, text);

                // 绘制右下角派系logo
                if (Logo != null)
                {
                    float logoSize = 32f;
                    Rect logoRect = new Rect(
                        localRect.xMax - logoSize - 4f,
                        localRect.yMax - logoSize - 4f,
                        logoSize,
                        logoSize
                    );
                    GUI.color = new Color(1f, 1f, 1f, 0.2f);
                    GUI.DrawTexture(logoRect, Logo);
                    GUI.color = Color.white;
                }

                Text.Anchor = TextAnchor.UpperLeft;
            }, doBackground: false, absorbInputAroundWindow: false, 0f);
        }
        #endregion
    }
}
