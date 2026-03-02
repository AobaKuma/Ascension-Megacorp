using Verse;
using System.Collections.Generic;

namespace USAC
{
    // USAC产品定义
    public class USACProductDef : Def
    {
        #region 基础定义
        public ThingDef thingDef;
        // 产品类别
        public string category; 
        public string subLabel;
        // 自定义图标路径
        public string iconPath; 
        #endregion

        #region 缓存逻辑
        private string cachedDescription;
        public string CachedDescription
        {
            get
            {
                if (cachedDescription == null)
                    cachedDescription = InternalUI.PortalUIUtility.FixCjkLineBreak(description ?? "");
                return cachedDescription;
            }
        }
        #endregion
    }
}
