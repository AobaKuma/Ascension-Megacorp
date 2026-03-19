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
        private const float COL_ICON = 40f;
        private const float COL_NAME = 130f;
        private const float COL_COUNT = 55f;
        private const float COL_PRICE = 65f;
        private const float COL_ADJUSTER = 115f;
        private const float COL_SPACING = 5f;
        #endregion

        #region 公共方法
        public static void DrawPlayerItemRow(Rect rect, Tradeable trad, int index, Action onChanged)
        {
            DrawRowBackground(rect, index);
            
            Rect inner = rect.ContractedBy(4);
            float x = inner.x;
            
            // 图标
            DrawItemIcon(new Rect(x, inner.y, COL_ICON - COL_SPACING, COL_ICON - COL_SPACING), trad);
            x += COL_ICON;
            
            // 物品名
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = trad.TraderWillTrade ? Color.white : ColTextMuted;
            
            string label = trad.Label;
            string displayLabel = TruncateMiddle(label, COL_NAME - COL_SPACING);
            
            Rect labelRect = new Rect(x, inner.y, COL_NAME - COL_SPACING, inner.height);
            Widgets.Label(labelRect, displayLabel);
            
            if (Mouse.IsOver(labelRect))
            {
                Widgets.DrawHighlight(labelRect);
                TooltipHandler.TipRegion(labelRect, () => label, trad.GetHashCode() * 2);
            }
            x += COL_NAME;
            
            // 持有数
            int colonyCount = trad.CountHeldBy(Transactor.Colony);
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(x, inner.y, COL_COUNT - COL_SPACING, inner.height), colonyCount.ToString());
            x += COL_COUNT;
            
            // 单价
            Text.Anchor = TextAnchor.MiddleCenter;
            if (trad.TraderWillTrade && colonyCount > 0)
            {
                float sellPrice = trad.GetPriceFor(TradeAction.PlayerSells);
                Text.Font = GameFont.Small;
                GUI.color = ColAccentCamo3;
                Widgets.Label(new Rect(x, inner.y, COL_PRICE - COL_SPACING, inner.height), sellPrice.ToString("F0"));
            }
            x += COL_PRICE;
            
            // 调整器
            if (trad.TraderWillTrade && colonyCount > 0)
            {
                Rect adjustRect = new(x, inner.y + 5, COL_ADJUSTER - COL_SPACING, 28);
                DrawCountAdjuster(adjustRect, trad, true, onChanged);
            }
            
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        public static void DrawTraderItemRow(Rect rect, Tradeable trad, int index, Action onChanged)
        {
            DrawRowBackground(rect, index);
            
            Rect inner = rect.ContractedBy(4);
            float x = inner.x;
            
            // 调整器
            if (trad.TraderWillTrade && trad.CountHeldBy(Transactor.Trader) > 0)
            {
                Rect adjustRect = new(x, inner.y + 5, COL_ADJUSTER - COL_SPACING, 28);
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
                Widgets.Label(new Rect(x, inner.y, COL_PRICE - COL_SPACING, inner.height), buyPrice.ToString("F0"));
            }
            x += COL_PRICE;
            
            // 库存
            if (trad.TraderWillTrade && trad.CountHeldBy(Transactor.Trader) > 0)
            {
                int traderCount = trad.CountHeldBy(Transactor.Trader);
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(x, inner.y, COL_COUNT - COL_SPACING, inner.height), traderCount.ToString());
            }
            x += COL_COUNT;
            
            // 物品名
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = trad.TraderWillTrade ? Color.white : ColTextMuted;
            
            string label = trad.Label;
            string displayLabel = TruncateMiddle(label, COL_NAME - COL_SPACING);
            
            Rect labelRect = new Rect(x, inner.y, COL_NAME - COL_SPACING, inner.height);
            Widgets.Label(labelRect, displayLabel);
            
            if (Mouse.IsOver(labelRect))
            {
                Widgets.DrawHighlight(labelRect);
                TooltipHandler.TipRegion(labelRect, () => label, trad.GetHashCode() * 3);
            }
            x += COL_NAME;
            
            // 图标
            DrawItemIcon(new Rect(x, inner.y, COL_ICON - COL_SPACING, COL_ICON - COL_SPACING), trad);
            
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
            
            Widgets.ThingIcon(iconRect, trad.AnyThing);
            
            if (Mouse.IsOver(iconRect))
            {
                TooltipHandler.TipRegionByKey(iconRect, "DefInfoTip");
                if (Widgets.ButtonInvisible(iconRect))
                    Find.WindowStack.Add(new Dialog_InfoCard(trad.AnyThing));
            }
        }

        private static string TruncateMiddle(string text, float maxWidth)
        {
            if (Text.CalcSize(text).x <= maxWidth)
                return text;
            
            // 逐步减少显示字符直到适合宽度
            int totalLen = text.Length;
            
            // 保留开头和结尾各至少3个字符
            for (int keepStart = Mathf.Max(3, totalLen / 2); keepStart >= 3; keepStart--)
            {
                for (int keepEnd = Mathf.Max(3, totalLen / 3); keepEnd >= 3; keepEnd--)
                {
                    if (keepStart + keepEnd >= totalLen - 1)
                        continue;
                    
                    string truncated = text.Substring(0, keepStart) + "..." + text.Substring(totalLen - keepEnd);
                    if (Text.CalcSize(truncated).x <= maxWidth)
                        return truncated;
                }
            }
            
            // 如果还是太长只保留开头3个字符
            return text.Substring(0, 3) + "...";
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
            
            if (DrawTacticalButton(arrowRect, arrowSymbol, canTransfer, GameFont.Medium, $"arrow_{tradeableId}"))
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
                    
                    // 使用 ClampAmount 确保在有效范围内
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
