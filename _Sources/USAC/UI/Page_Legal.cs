using UnityEngine;
using Verse;
using static USAC.InternalUI.PortalUIUtility;

namespace USAC.InternalUI
{
    // USAC 法律与合规页面
    public class Page_Legal : IPortalPage
    {
        public string Title => "USAC.UI.Legal.Title".Translate();
        private Vector2 scrollPos;

        public void Draw(Rect rect, Dialog_USACPortal parent)
        {
            // 动态计算视图高度
            float footerH = Text.CalcHeight("USAC.UI.Legal.Footer".Translate(), rect.width - 16);
            float viewH = Mathf.Max(rect.height, 50 + 130 * 2 + footerH + 40);
            Widgets.BeginScrollView(rect, ref scrollPos, new Rect(0, 0, rect.width - 16, viewH));
            float y = 0;

            Text.Font = GameFont.Medium;
            GUI.color = ColAccentCamo1;
            Widgets.Label(new Rect(0, y, rect.width, 40), "USAC.UI.Legal.Ordinance".Translate());
            y += 50;

            DrawInfoCard(ref y, rect.width, "USAC.UI.Legal.Clause01.Title".Translate(), "USAC.UI.Legal.Clause01.Desc".Translate());
            DrawInfoCard(ref y, rect.width, "USAC.UI.Legal.Clause02.Title".Translate(), "USAC.UI.Legal.Clause02.Desc".Translate());

            GUI.color = ColTextMuted;
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(0, y, rect.width - 16, footerH), "USAC.UI.Legal.Footer".Translate());

            Widgets.EndScrollView();
        }
    }
}
