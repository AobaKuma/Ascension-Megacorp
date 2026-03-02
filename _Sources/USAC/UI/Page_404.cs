using UnityEngine;
using Verse;
using static USAC.InternalUI.PortalUIUtility;

namespace USAC.InternalUI
{
    // USAC 404 错误页面
    public class Page_404 : IPortalPage
    {
        public string Title => "USAC.UI.Error.404.Title".Translate();

        public void Draw(Rect rect, Dialog_USACPortal parent)
        {
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Medium;
            GUI.color = ColAccentRed;
            Widgets.Label(rect, "USAC.UI.Error.404.Text".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }
    }
}
