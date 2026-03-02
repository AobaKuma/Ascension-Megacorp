using UnityEngine;
using Verse;

namespace USAC.InternalUI
{
    // USAC 门户页面接口
    public interface IPortalPage
    {
        string Title { get; }
        void Draw(Rect rect, Dialog_USACPortal parent);
    }
}
