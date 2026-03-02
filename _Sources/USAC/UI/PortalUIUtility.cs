using UnityEngine;
using Verse;
using RimWorld;

namespace USAC.InternalUI
{
    // UI绘制工具类
    public static class PortalUIUtility
    {
        #region 颜色
        public static readonly Color ColWindowBg = new(0.12f, 0.12f, 0.13f, 1f);
        public static readonly Color ColHeaderBg = new(0.18f, 0.18f, 0.19f, 1f);
        public static readonly Color ColAddressBar = new(0.06f, 0.06f, 0.07f, 0.9f);
        public static readonly Color ColAccentCamo1 = new(0.75f, 0.69f, 0.62f);
        public static readonly Color ColAccentCamo2 = new(0.61f, 0.55f, 0.51f);
        public static readonly Color ColAccentCamo3 = new(0.85f, 0.85f, 0.85f);
        public static readonly Color ColAccentRed = new(0.6f, 0.25f, 0.25f);
        public static readonly Color ColTextActive = new(0.9f, 0.9f, 0.9f);
        public static readonly Color ColTextMuted = new(0.65f, 0.65f, 0.65f);
        public static readonly Color ColBorder = new(1f, 1f, 1f, 0.06f);
        #endregion

        #region 动效系统
        private static readonly System.Collections.Generic.Dictionary<string, float> animStates = new();
        private const float LerpSpeed = 10f;
        private static int _animCleanFrame;

        // 获取动效进度
        private static float GetAnimAlpha(string key, bool active)
        {
            if (key == null) return 0f;
            if (Time.frameCount - _animCleanFrame > 600) PruneAnimStates();
            if (!animStates.TryGetValue(key, out float val)) val = 0f;
            val = Mathf.Lerp(val, active ? 1f : 0f, Time.unscaledDeltaTime * LerpSpeed);
            animStates[key] = val;
            return val;
        }

        // 清理已静止的动效条目
        private static void PruneAnimStates()
        {
            _animCleanFrame = Time.frameCount;
            var dead = new System.Collections.Generic.List<string>(4);
            foreach (var kv in animStates)
                if (kv.Value < 0.001f) dead.Add(kv.Key);
            foreach (var k in dead) animStates.Remove(k);
        }
        #endregion

        // 绘制信息卡片
        public static void DrawInfoCard(ref float y, float width, string title, string body)
        {
            Rect r = new(0, y, width - 30, 110);
            DrawBentoBox(r, (boxRect) =>
            {
                Rect inner = boxRect.ContractedBy(15);
                DrawColoredLabel(inner.TopPartPixels(30), title.ToUpper(), ColAccentCamo1, GameFont.Small);
                DrawColoredLabel(new Rect(inner.x, inner.y + 35, inner.width, 60), body, ColTextActive, GameFont.Tiny);
            });
            y += 130;
        }

        // 战术按钮绘制
        public static bool DrawTacticalButton(Rect r, string label, bool active = true, GameFont font = GameFont.Small, string key = null)
        {
            bool hover = active && Mouse.IsOver(r);
            float alpha = GetAnimAlpha(key ?? label, hover);

            // 计算背景颜色
            Color bg = active ? Color.Lerp(ColHeaderBg, new Color(0.25f, 0.25f, 0.26f, 1f), alpha) : new Color(0.1f, 0.1f, 0.1f, 1f);
            Widgets.DrawBoxSolid(r, bg);

            GUI.color = active ? Color.Lerp(ColAccentCamo1, Color.white, alpha) : ColTextMuted;
            Widgets.DrawBox(r, 1);
            GUI.color = Color.white;

            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = font;
            GUI.color = active ? (alpha > 0.5f ? Color.white : ColAccentCamo1) : ColTextMuted;
            Widgets.Label(r, label);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            return active && Widgets.ButtonInvisible(r);
        }

        // Bento容器绘制
        public static bool DrawBentoBox(Rect r, System.Action<Rect> drawContent, bool clickable = true, string key = null)
        {
            bool hover = clickable && Mouse.IsOver(r);
            float alpha = GetAnimAlpha(key, hover);

            // 物理扩容层渲染
            float expandPx = 3f * alpha;
            Rect drawR = r.ExpandedBy(expandPx);

            // 绘制背景与边框
            Color hoverSubstrate = new(0.22f, 0.22f, 0.23f, 1f);
            Widgets.DrawBoxSolid(drawR, Color.Lerp(ColHeaderBg, hoverSubstrate, alpha));

            GUI.color = ColBorder;
            Widgets.DrawBox(drawR, 1);
            GUI.color = Color.Lerp(Color.clear, Color.white, alpha);
            Widgets.DrawBox(drawR, 1);

            if (alpha > 0.01f)
            {
                DrawCorner(drawR, Color.white, alpha);
            }
            GUI.color = Color.white;

            // 矩阵缩放层渲染
            Matrix4x4 prevMatrix = GUI.matrix;
            if (alpha > 0.001f)
            {
                // 执行对齐缩放
                float contentScale = 1f + (0.02f * alpha);
                // 执行矩阵变换
                GUIUtility.ScaleAroundPivot(new Vector2(contentScale, contentScale), r.center);
            }

            // 执行内部渲染闭包
            drawContent?.Invoke(r);

            // 恢复矩阵防止污染渲染
            GUI.matrix = prevMatrix;

            // 事件判定依然基于原始矩形
            return clickable && Widgets.ButtonInvisible(r);
        }

        private static void DrawCorner(Rect r, Color color, float alpha)
        {
            GUI.color = new Color(color.r, color.g, color.b, alpha);
            float s = 8f;
            // 绘制装饰角
            Widgets.DrawLineHorizontal(r.x, r.y, s);
            Widgets.DrawLineVertical(r.x, r.y, s);
            Widgets.DrawLineHorizontal(r.xMax - s, r.yMax, s);
            Widgets.DrawLineVertical(r.xMax, r.yMax - s, s);
            GUI.color = Color.white;
        }

        // 绘制带边框的文本
        public static void DrawColoredLabel(Rect r, string text, Color color, GameFont font = GameFont.Small, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            Text.Font = font;
            Text.Anchor = anchor;
            GUI.color = color;
            Widgets.Label(r, FixCjkLineBreak(text));
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }

        private static readonly System.Collections.Generic.Dictionary<string, string> cjkCache = new();
        private static readonly System.Text.RegularExpressions.Regex cjkRegex = new(
            @"(?<=[\u4e00-\u9fa5])\s+(?=[a-zA-Z0-9])|(?<=[a-zA-Z0-9])\s+(?=[\u4e00-\u9fa5])",
            System.Text.RegularExpressions.RegexOptions.Compiled);

        // 处理中英混排换行错乱
        public static string FixCjkLineBreak(string text)
        {
            if (text.NullOrEmpty()) return text;
            if (cjkCache.TryGetValue(text, out var cached)) return cached;
            var result = cjkRegex.Replace(text, "");
            cjkCache[text] = result;
            return result;
        }
        #region 资源缓存
        private struct PreviewCache
        {
            public Texture2D bodyTex;
            public Color renderColor;
            public Texture2D headTex;
            public Vector3 headOff;
            public Vector2 headSize;
            public Texture2D turretTex;
        }
        private static readonly System.Collections.Generic.Dictionary<string, PreviewCache> previewCache = new();
        #endregion

        // 快速设置透明度
        public static Color ToTransp(this Color c, float a) => new(c.r, c.g, c.b, a);

        // 绘制产品预览图
        public static void DrawProductPreview(Rect drawArea, USACProductDef product, float alpha = 1f)
        {
            if (product == null) return;
            if (!previewCache.TryGetValue(product.defName, out var cache))
            {
                cache = CreatePreviewCache(product);
                previewCache[product.defName] = cache;
            }

            if (cache.bodyTex != null)
            {
                DrawBodyAndParts(drawArea, cache, alpha);
            }
            else if (product.thingDef != null)
            {
                GUI.color = product.thingDef.uiIconColor.ToTransp(alpha);
                GUI.DrawTexture(drawArea, product.thingDef.uiIcon, ScaleMode.ScaleToFit);
                GUI.color = Color.white;
            }
        }

        private static PreviewCache CreatePreviewCache(USACProductDef product)
        {
            var cache = new PreviewCache { renderColor = Color.white };
            var orderExt = product.thingDef?.GetModExtension<ModExtension_MechOrder>();
            var kindDef = orderExt?.mechKindDef;
            var raceDef = kindDef?.race;
            if (kindDef == null) return cache;

            var paint = raceDef?.GetCompProperties<Fortified.CompProperties_Paintable>();
            var mExt = raceDef?.GetModExtension<Fortified.HumanlikeMechExtension>();
            bool isHum = mExt != null || (raceDef?.defName?.Contains("Rocky") ?? false);
            if (paint != null && !isHum) cache.renderColor = paint.defaultColor;

            var bData = kindDef.lifeStages[0].bodyGraphicData;
            cache.bodyTex = bData?.Graphic?.MatSouth?.mainTexture as Texture2D;
            if (mExt?.headGraphic != null)
            {
                cache.headTex = mExt.headGraphic.Graphic?.MatSouth?.mainTexture as Texture2D;
                cache.headOff = new Vector3(0, 0, 0.342f + mExt.headOffset.z);
                cache.headSize = mExt.headGraphic.drawSize;
            }
            cache.turretTex = raceDef?.GetCompProperties<Fortified.CompProperties_VehicleWeapon>()?.defaultWeapon?.uiIcon;
            return cache;
        }

        private static void DrawBodyAndParts(Rect drawArea, PreviewCache cache, float alpha)
        {
            float aspect = (float)cache.bodyTex.width / cache.bodyTex.height;
            Rect bRect = (drawArea.width / drawArea.height > aspect)
                ? new Rect(drawArea.center.x - (drawArea.height * aspect) / 2f, drawArea.y, drawArea.height * aspect, drawArea.height)
                : new Rect(drawArea.x, drawArea.center.y - (drawArea.width / aspect) / 2f, drawArea.width, drawArea.width / aspect);

            GUI.color = cache.renderColor.ToTransp(alpha);
            GUI.DrawTexture(bRect, cache.bodyTex);

            if (cache.headTex != null)
            {
                float hSz = bRect.width * (cache.headSize.x / 1.5f);
                float hOffY = (cache.headOff.z / 1.5f) * bRect.height;
                Rect hRect = new(bRect.center.x - hSz / 2f, bRect.center.y - hSz / 2f - hOffY, hSz, hSz);
                GUI.color = Color.white.ToTransp(alpha);
                GUI.DrawTexture(hRect, cache.headTex);
            }
            if (cache.turretTex != null)
            {
                float tSz = bRect.width * 0.6f;
                GUI.color = Color.white.ToTransp(alpha);
                GUI.DrawTexture(new Rect(bRect.xMax - tSz * 0.8f, bRect.center.y - tSz / 2f, tSz, tSz), cache.turretTex);
            }
            GUI.color = Color.white;
        }

        private static readonly System.Collections.Generic.Dictionary<int, Texture2D> gradientCache = new();

        // 获取渐变纹理缓存
        private static Texture2D GetGradientTex(Color c1, Color c2)
        {
            int hash = (c1.GetHashCode() * 397) ^ c2.GetHashCode();
            if (gradientCache.TryGetValue(hash, out var tex) && tex != null) return tex;
            tex = new Texture2D(1, 64, TextureFormat.RGBA32, false);
            for (int y = 0; y < 64; y++)
                tex.SetPixel(0, y, Color.Lerp(c1, c2, y / 63f));
            tex.Apply();
            gradientCache[hash] = tex;
            return tex;
        }

        // 绘制渐变背景
        public static void DrawUIGradient(Rect r, Color c1, Color c2)
        {
            GUI.DrawTexture(r, GetGradientTex(c1, c2));
        }
    }
}
