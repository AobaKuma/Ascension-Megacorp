using System;
using UnityEngine;
using Verse;
using RimWorld;
using static USAC.InternalUI.PortalUIUtility;

namespace USAC
{
    // 左右栏物品行绘制组件
    public static class USACTradePanel
    {
        #region 字段
        private static readonly System.Collections.Generic.Dictionary<int, int> inputValues = new();
        
        // 列宽常量 确保左右对称
        public const float COL_ICON = 45f;
        public const float COL_COUNT = 55f;
        public const float COL_PRICE = 65f;
        public const float COL_ADJUSTER = 90f;
        public const float COL_SPACING = 0f;

        public static float CalcNameWidth(float totalWidth)
        {
            return Mathf.Max(100f, totalWidth - COL_ICON - COL_COUNT - COL_PRICE - COL_ADJUSTER);
        }
        #endregion

        #region 公共方法
        public static void DrawPlayerItemRow(Rect rect, Tradeable trad, int index, Action onChanged)
        {
            DrawRowBackground(rect, index);
            
            float x = rect.x;
            string label = trad.Label;
            
            // 图标填满高度
            Rect iconRect = new(x, rect.y, rect.height, rect.height);
            if (Mouse.IsOver(iconRect))
            {
                Widgets.DrawHighlight(iconRect);
                USAC.TooltipHandler.DrawInstantTooltip(label);
            }
            DrawItemIcon(iconRect, trad);
            x += COL_ICON;
            
            // 物品名独立高亮
            float colNameWidth = CalcNameWidth(rect.width);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = trad.TraderWillTrade ? Color.white : ColTextMuted;
            
            string displayLabel = TruncateMiddle(label, colNameWidth);
            
            Rect nameRect = new(x, rect.y, colNameWidth, rect.height);
            if (Mouse.IsOver(nameRect))
            {
                Widgets.DrawHighlight(nameRect);
                USAC.TooltipHandler.DrawInstantTooltip(label);
            }
            
            // 绘制文本 带左边距设计说明
            Rect labelTextRect = nameRect;
            labelTextRect.xMin += 5f;
            Widgets.Label(labelTextRect, displayLabel);
            x += colNameWidth;
            
            // 持有数
            int colonyCount = trad.CountHeldBy(Transactor.Colony);
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(x, rect.y, COL_COUNT, rect.height), colonyCount.ToString());
            x += COL_COUNT;
            
            // 单价
            Text.Anchor = TextAnchor.MiddleCenter;
            if (trad.TraderWillTrade && colonyCount > 0)
            {
                float sellPrice = trad.GetPriceFor(TradeAction.PlayerSells);
                Text.Font = GameFont.Small;
                GUI.color = ColAccentCamo3;
                Widgets.Label(new Rect(x, rect.y, COL_PRICE, rect.height), sellPrice.ToString("F0"));
            }
            x += COL_PRICE;
            
            // 调整器
            if (trad.TraderWillTrade && colonyCount > 0)
            {
                Rect adjustRect = new(x + 5, rect.y + (rect.height - 28) / 2f, COL_ADJUSTER - 10f, 28);
                DrawCountAdjuster(adjustRect, trad, true, onChanged);
            }
            
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        public static void DrawTraderItemRow(Rect rect, Tradeable trad, int index, Action onChanged)
        {
            DrawRowBackground(rect, index);
            
            float x = rect.x;
            
            // 调整器 (带垂直居中修正)
            if (trad.TraderWillTrade && trad.CountHeldBy(Transactor.Trader) > 0)
            {
                Rect adjustRect = new(x + 5, rect.y + (rect.height - 28) / 2f, COL_ADJUSTER - 10f, 28);
                DrawCountAdjuster(adjustRect, trad, false, onChanged);
            }
            x += COL_ADJUSTER;
            
            // 单价
            if (trad.TraderWillTrade && trad.CountHeldBy(Transactor.Trader) > 0)
            {
                float buyPrice = trad.GetPriceFor(TradeAction.PlayerBuys);
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = ColAccentCamo3;
                Widgets.Label(new Rect(x, rect.y, COL_PRICE, rect.height), buyPrice.ToString("F0"));
            }
            x += COL_PRICE;
            
            // 库存
            if (trad.TraderWillTrade && trad.CountHeldBy(Transactor.Trader) > 0)
            {
                int traderCount = trad.CountHeldBy(Transactor.Trader);
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(x, rect.y, COL_COUNT, rect.height), traderCount.ToString());
            }
            x += COL_COUNT;
            
            // 物品名
            float colNameWidth = CalcNameWidth(rect.width);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = trad.TraderWillTrade ? Color.white : ColTextMuted;
            
            string label = trad.Label;
            string displayLabel = TruncateMiddle(label, colNameWidth);
            
            Rect nameRect = new(x, rect.y, colNameWidth, rect.height);
            if (Mouse.IsOver(nameRect))
            {
                Widgets.DrawHighlight(nameRect);
                USAC.TooltipHandler.DrawInstantTooltip(label);
            }
            
            // 文本右对齐一点
            Rect labelTextRect = nameRect;
            labelTextRect.xMax -= 5f;
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(labelTextRect, displayLabel);

            // 渲染独立图标 填满最右侧说明
            Rect iconRect = new(rect.xMax - rect.height, rect.y, rect.height, rect.height);
            if (Mouse.IsOver(iconRect))
            {
                Widgets.DrawHighlight(iconRect);
                USAC.TooltipHandler.DrawInstantTooltip(label);
            }
            DrawItemIcon(iconRect, trad);
            
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }
        #endregion

        #region 私有方法
        private static void DrawRowBackground(Rect rect, int index)
        {
            if (index % 2 == 0)
                Widgets.DrawBoxSolid(rect, new Color(1, 1, 1, 0.02f));
            
            if (Mouse.IsOver(rect))
                Widgets.DrawBoxSolid(rect, new Color(1, 1, 1, 0.05f));
        }

        private static void DrawItemIcon(Rect iconRect, Tradeable trad)
        {
            if (trad.AnyThing == null) return;

            // 根据物理尺寸计算缩放 防止溢出
            float scale = 0.9f;
            if (trad.ThingDef?.graphicData != null)
            {
                var drawSize = trad.ThingDef.graphicData.drawSize;
                float maxSide = Mathf.Max(drawSize.x, drawSize.y);
                if (maxSide > 1f)
                {
                    scale /= maxSide;
                }
            }

            Widgets.ThingIcon(iconRect.ScaledBy(scale), trad.AnyThing);

            if (Mouse.IsOver(iconRect))
            {
                Verse.TooltipHandler.TipRegionByKey(iconRect, "DefInfoTip");
                if (Widgets.ButtonInvisible(iconRect))
                    Find.WindowStack.Add(new Dialog_InfoCard(trad.AnyThing));
            }
        }

        private static string TruncateMiddle(string text, float maxWidth)
        {
            if (Text.CalcSize(text).x <= maxWidth) return text;

            // 预处理颜色标签
            string stripped = text.StripTags();
            if (Text.CalcSize(stripped).x <= maxWidth) return stripped;

            int len = stripped.Length;
            // 寻找最佳截断点 基于纯文本
            for (int k = len - 1; k >= 6; k--)
            {
                int left = k / 2;
                int right = k - left;
                string res = stripped.Substring(0, left) + "..." + stripped.Substring(len - right);
                if (Text.CalcSize(res).x <= maxWidth) return res;
            }

            return stripped.Substring(0, Math.Min(len, 3)) + "...";
        }

        private static void DrawCountAdjuster(Rect rect, Tradeable trad, bool isPlayerSide, Action onChanged)
        {
            int tradeableId = trad.GetHashCode();
            if (!inputValues.ContainsKey(tradeableId))
                inputValues[tradeableId] = 0;
            
            int inputCount = inputValues[tradeableId];
            
            float arrowWidth = 32f;
            float inputWidth = rect.width - arrowWidth - 4f;
            
            Rect arrowRect = isPlayerSide 
                ? new Rect(rect.xMax - arrowWidth, rect.y, arrowWidth, rect.height)
                : new Rect(rect.x, rect.y, arrowWidth, rect.height);
            
            Rect inputRect = isPlayerSide
                ? new Rect(rect.x, rect.y, inputWidth, rect.height)
                : new Rect(rect.x + arrowWidth + 4f, rect.y, inputWidth, rect.height);
            
            Widgets.DrawBoxSolidWithOutline(inputRect, new Color(0.06f, 0.06f, 0.07f, 0.9f), ColBorder);
            
            // 数字输入框
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            
            bool hasCustomInput = inputCount > 0;
            GUI.color = hasCustomInput ? ColAccentCamo3 : new Color(1, 1, 1, 0.3f);
            
            string buffer = hasCustomInput ? inputCount.ToString() : "1";
            string newBuffer = Widgets.TextField(inputRect, buffer, 6, new System.Text.RegularExpressions.Regex(@"^\d*$"));
            
            if (newBuffer != buffer)
            {
                if (string.IsNullOrEmpty(newBuffer))
                {
                    inputValues[tradeableId] = 0;
                }
                else if (int.TryParse(newBuffer, out int newValue))
                {
                    int maxAvailable = isPlayerSide
                        ? trad.CountHeldBy(Transactor.Colony)
                        : trad.CountHeldBy(Transactor.Trader);
                    inputValues[tradeableId] = Mathf.Clamp(newValue, 0, maxAvailable);
                }
            }
            
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            
            // 箭头按钮
            string arrowSymbol = isPlayerSide ? "→" : "←";
            bool canTransfer = isPlayerSide 
                ? trad.CountHeldBy(Transactor.Colony) > 0
                : trad.CountHeldBy(Transactor.Trader) > 0;
            string sideKey = isPlayerSide ? "p" : "t";
            if (DrawTacticalButton(arrowRect, arrowSymbol, canTransfer, GameFont.Medium, $"arrow_{sideKey}_{tradeableId}"))
            {
                int amountToAdd = inputValues[tradeableId] > 0 ? inputValues[tradeableId] : 1;
                
                int maxAvailable = isPlayerSide
                    ? trad.CountHeldBy(Transactor.Colony)
                    : trad.CountHeldBy(Transactor.Trader);
                
                amountToAdd = Mathf.Min(amountToAdd, maxAvailable);
                
                if (amountToAdd > 0)
                {
                    int currentCount = trad.CountToTransfer;
                    int newTotal = isPlayerSide 
                        ? currentCount - amountToAdd
                        : currentCount + amountToAdd;
                    
                    // 值边界保护
                    int clampedTotal = trad.ClampAmount(newTotal);
                    
                    // 仅在值有效时调用
                    if (trad.CanAdjustTo(clampedTotal).Accepted)
                    {
                        trad.AdjustTo(clampedTotal);
                        onChanged?.Invoke();
                    }
                }
            }
        }

        private static int GetDisplayCount(int countToTransfer, bool isPlayerSide)
        {
            if (countToTransfer == 0) return 0;
            
            if (isPlayerSide)
                return countToTransfer < 0 ? Math.Abs(countToTransfer) : 0;
            else
                return countToTransfer > 0 ? countToTransfer : 0;
        }

        public static void ClearInputValues()
        {
            inputValues.Clear();
        }
        #endregion
    }

    // 数字输入对话框
    internal class Dialog_NumberInput : Window
    {
        private readonly Tradeable tradeable;
        private readonly bool isPlayerSide;
        private readonly Action<int> onValueSet;
        private string inputBuffer;

        public override Vector2 InitialSize => new Vector2(300f, 180f);

        public Dialog_NumberInput(Tradeable trad, bool playerSide, Action<int> onSet)
        {
            tradeable = trad;
            isPlayerSide = playerSide;
            onValueSet = onSet;
            inputBuffer = "";
            
            doCloseX = true;
            forcePause = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Small;
            
            Widgets.Label(new Rect(0, 0, inRect.width, 30), tradeable.Label);
            
            int maxCount = isPlayerSide
                ? tradeable.CountHeldBy(Transactor.Colony)
                : tradeable.CountHeldBy(Transactor.Trader);
            
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            Widgets.Label(new Rect(0, 30, inRect.width, 20), "USAC.Trade.Dialog.Available".Translate(maxCount));
            GUI.color = Color.white;
            
            Text.Font = GameFont.Small;
            inputBuffer = Widgets.TextField(new Rect(0, 60, inRect.width, 35), inputBuffer);
            
            if (Widgets.ButtonText(new Rect(0, inRect.height - 35, inRect.width / 2 - 5, 35), "USAC.Trade.Dialog.Cancel".Translate()))
            {
                Close();
            }
            
            if (Widgets.ButtonText(new Rect(inRect.width / 2 + 5, inRect.height - 35, inRect.width / 2 - 5, 35), "USAC.Trade.Dialog.Confirm".Translate()))
            {
                if (int.TryParse(inputBuffer, out int value))
                {
                    value = Mathf.Clamp(value, 0, maxCount);
                    onValueSet?.Invoke(value);
                }
                Close();
            }
        }
    }
}
