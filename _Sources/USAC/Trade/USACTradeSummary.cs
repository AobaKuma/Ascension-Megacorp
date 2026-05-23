using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
using static USAC.InternalUI.PortalUIUtility;

namespace USAC
{
    // 交易摘要中栏组件
    public static class USACTradeSummary
    {
        #region 字段
        private static Vector2 scrollPosition;
        private static List<Tradeable> activeTradeables = new();
        private static float totalBuyValue;
        private static float totalSellValue;
        private static float projectedConsumableValue; // 预览实际预计消耗的货币价值
        private static int currentTipIndex = 0;
        private static readonly System.Collections.Generic.Dictionary<int, int> inputValues = new();

        // 面板布局常量
        private const float PANEL_PAD = 12f;        // ContractedBy 边距
        private const float ROW_TINY = 20f;         // Tiny 字号行高
        private const float ROW_SMALL = 26f;        // Small 字号行高
        private const float ROW_TITLE = 24f;        // 货币标题行高
        private const float ROW_SUB = 18f;          // 副文本行高
        private const float DIVIDER_PAD = 6f;       // 分隔线上下间距合计
        private const float ROW_GAP = 2f;           // 同组行间额外间距
        private const float TIP_GAP = 6f;           // 小贴士与上方分隔
        #endregion

        #region 公共方法
        public static void Refresh(List<Tradeable> allTradeables, Tradeable currency)
        {
            activeTradeables.Clear();
            totalBuyValue = 0f;
            totalSellValue = 0f;
            projectedConsumableValue = 0f;
            
            if (allTradeables == null) return;
            
            foreach (var trad in allTradeables)
            {
                if (trad.CountToTransfer != 0)
                {
                    activeTradeables.Add(trad);
                    
                    if (trad.CountToTransfer > 0)
                        totalBuyValue += trad.CurTotalCurrencyCostForSource;
                    else if (trad.CountToTransfer < 0)
                        totalSellValue += trad.CurTotalCurrencyCostForDestination;
                }
            }

            // 计算预计消耗货币价值
            if (currency is Tradeable_USACCurrency usacCurrency)
            {
                projectedConsumableValue = CalculateProjectedConsumption(totalBuyValue - totalSellValue);
            }
            
            // 随机更新小贴士
            currentTipIndex = Rand.Range(0, 5);
        }

        public static void Draw(Rect rect, Tradeable currency, System.Action onChanged)
        {
            Widgets.DrawBoxSolidWithOutline(rect, new Color(0, 0, 0, 0.2f), ColBorder);

            Rect headerRect = new(rect.x, rect.y, rect.width, 35);
            DrawHeader(headerRect);

            float footerHeight = CalcFooterHeight(rect.width);
            Rect listRect = new(rect.x, rect.y + 40, rect.width, rect.height - 40 - footerHeight);
            DrawTradeList(listRect, onChanged);

            Rect footerRect = new(rect.x, rect.yMax - footerHeight, rect.width, footerHeight);
            DrawCurrencyPanel(footerRect, currency);
        }

        // 按显示内容动态计算面板高度 与 DrawCurrencyPanel 行布局严格对应
        private static float CalcFooterHeight(float panelWidth)
        {
            float h = PANEL_PAD * 2f;        // 上下内边距
            h += ROW_TITLE;                  // 货币标题
            h += DIVIDER_PAD;                // 分隔线1
            h += ROW_TINY;                   // 买入
            h += ROW_TINY + ROW_GAP;         // 卖出
            h += ROW_SMALL;                  // 结算

            float netBill = totalBuyValue - totalSellValue;
            float wastage = projectedConsumableValue - netBill;
            if (wastage > 0.5f && netBill > 0) h += ROW_SUB;

            h += DIVIDER_PAD;                // 分隔线2
            h += ROW_TINY + ROW_GAP;         // 当前资产
            h += ROW_SMALL;                  // 交易后剩余

            if (activeTradeables.Count == 0) h += TIP_GAP + CalcTipHeight(panelWidth - PANEL_PAD * 2f);
            return h;
        }

        private static float CalcTipHeight(float innerWidth)
        {
            GameFont prevFont = Text.Font;
            bool prevWrap = Text.WordWrap;
            Text.Font = GameFont.Tiny;
            Text.WordWrap = true;
            float h = Text.CalcHeight(
                $"USAC.Trade.Summary.Tip_{currentTipIndex}".Translate(),
                innerWidth);
            Text.Font = prevFont;
            Text.WordWrap = prevWrap;
            return h;
        }
        #endregion

        #region 私有方法
        private static void DrawHeader(Rect rect)
        {
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = ColAccentCamo1;
            Widgets.Label(rect, "USAC.Trade.Summary".Translate());
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private static void DrawTradeList(Rect rect, System.Action onChanged)
        {
            Rect inner = rect.ContractedBy(5);
            float viewHeight = activeTradeables.Count * 45f + 10f;
            // 仅当内容溢出时才让出滚动条宽度 否则铺满
            float scrollbarReserve = viewHeight > inner.height ? 20f : 4f;
            Rect viewRect = new(0, 0, inner.width - scrollbarReserve, viewHeight);

            Widgets.BeginScrollView(inner, ref scrollPosition, viewRect);

            float y = 5f;
            var tradeCopy = activeTradeables.ToList();
            foreach (var trad in tradeCopy)
            {
                Rect rowRect = new(5, y, viewRect.width - 10, 40);
                DrawSummaryRow(rowRect, trad, onChanged);
                y += 45f;
            }

            Widgets.EndScrollView();
            
            if (activeTradeables.Count == 0)
            {
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = ColTextMuted;
                Widgets.Label(inner, "USAC.Trade.NoActiveTrades".Translate());
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
            }
        }

        private static void DrawSummaryRow(Rect rect, Tradeable trad, System.Action onChanged)
        {
            Widgets.DrawBoxSolidWithOutline(rect, new Color(0, 0, 0, 0.3f), ColBorder);

            Rect inner = rect.ContractedBy(4);
            bool isSelling = trad.CountToTransfer < 0;

            int tradeableId = trad.GetHashCode();
            if (!inputValues.ContainsKey(tradeableId))
                inputValues[tradeableId] = 0;

            int inputCount = inputValues[tradeableId];

            // 调整器区域（需要排除在Tooltip外）
            float adjustWidth = 80f;
            float adjustX = inner.xMax - adjustWidth;
            Rect adjustRect = new(adjustX, inner.y + 4, adjustWidth, 28);

            // 可交互区域（整行除了调整器）
            Rect interactiveRect = new(inner.x, inner.y, adjustX - inner.x - 2, inner.height);

            // 即时Tooltip 无延迟显示
            if (Mouse.IsOver(interactiveRect))
            {
                Widgets.DrawHighlight(interactiveRect);

                string tip = trad.LabelCap;
                if (!trad.TipDescription.NullOrEmpty())
                    tip += "\n\n" + trad.TipDescription;
                tip += "\n\n" + (isSelling
                    ? "USAC.Trade.Summary.SellingTo".Translate(TradeSession.trader.TraderName)
                    : "USAC.Trade.Summary.BuyingFrom".Translate(TradeSession.trader.TraderName));

                // 使用即时显示模式 绕过延迟
                TooltipHandler.DrawInstantTooltip(tip);
            }

            // 图标
            if (trad.AnyThing != null)
            {
                Rect iconRect = new(inner.x + 5, inner.y, 32, 32);
                Widgets.ThingIcon(iconRect, trad.AnyThing);
            }

            // 方向箭头
            GUI.color = isSelling ? new Color(1f, 0.6f, 0.6f) : new Color(0.6f, 1f, 0.6f);
            string directionArrow = isSelling ? "↑" : "↓";
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(new Rect(inner.x + 42, inner.y, 20, inner.height), directionArrow);
            GUI.color = Color.white;

            // 数量显示
            int absCount = Mathf.Abs(trad.CountToTransfer);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(new Rect(inner.x + 67, inner.y, 35, inner.height), absCount.ToString());

            // 物品名称缩略
            float nameStartX = inner.x + 107;
            float nameWidth = adjustX - nameStartX - 5;

            if (nameWidth > 30)
            {
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleLeft;
                GUI.color = ColTextMuted;
                Text.WordWrap = false;

                string label = trad.Label;
                string truncated = TruncateLabel(label, nameWidth);

                Rect nameRect = new(nameStartX, inner.y, nameWidth, inner.height);
                Widgets.Label(nameRect, truncated);

                GUI.color = Color.white;
            }

            // 调整器
            DrawReduceAdjuster(adjustRect, trad, isSelling, onChanged);

            Text.Anchor = TextAnchor.UpperLeft;
            Text.WordWrap = true;
        }

        private static string TruncateLabel(string label, float maxWidth)
        {
            if (string.IsNullOrEmpty(label)) return "";

            Text.Font = GameFont.Tiny;
            float fullWidth = Text.CalcSize(label).x;

            if (fullWidth <= maxWidth) return label;

            // 截断并添加省略号
            int len = label.Length;
            for (int i = len - 1; i > 0; i--)
            {
                string truncated = label.Substring(0, i) + "...";
                if (Text.CalcSize(truncated).x <= maxWidth)
                    return truncated;
            }

            return "...";
        }

        private static void DrawReduceAdjuster(Rect rect, Tradeable trad, bool isSelling, System.Action onChanged)
        {
            int tradeableId = trad.GetHashCode();
            int inputCount = inputValues[tradeableId];
            
            float arrowWidth = 28f;
            float inputWidth = rect.width - arrowWidth - 2f;
            
            // 箭头方向
            string arrowSymbol = isSelling ? "←" : "→";
            
            Rect inputRect = new Rect(rect.x, rect.y, inputWidth, rect.height);
            Rect arrowRect = new Rect(rect.x + inputWidth + 2f, rect.y, arrowWidth, rect.height);
            
            // 输入框
            Widgets.DrawBoxSolidWithOutline(inputRect, new Color(0.06f, 0.06f, 0.07f, 0.9f), ColBorder);
            
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = inputCount > 0 ? ColAccentCamo3 : ColTextMuted;
            
            string buffer = inputCount > 0 ? inputCount.ToString() : "1";
            string newBuffer = Widgets.TextField(inputRect, buffer, 6, new System.Text.RegularExpressions.Regex(@"^\d*$"));
            
            if (newBuffer != buffer)
            {
                if (string.IsNullOrEmpty(newBuffer))
                {
                    inputValues[tradeableId] = 0;
                }
                else if (int.TryParse(newBuffer, out int newValue))
                {
                    int maxReduce = Mathf.Abs(trad.CountToTransfer);
                    inputValues[tradeableId] = Mathf.Clamp(newValue, 0, maxReduce);
                }
            }
            
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            
            // 箭头按钮
            if (DrawTacticalButton(arrowRect, arrowSymbol, true, GameFont.Medium, $"reduce_{tradeableId}"))
            {
                int amountToReduce = inputValues[tradeableId] > 0 ? inputValues[tradeableId] : 1;
                int currentCount = trad.CountToTransfer;
                int maxReduce = Mathf.Abs(currentCount);
                
                amountToReduce = Mathf.Min(amountToReduce, maxReduce);
                
                if (amountToReduce > 0)
                {
                    int newTotal;
                    if (isSelling)
                    {
                        // 出售数量修正
                        newTotal = currentCount + amountToReduce;
                    }
                    else
                    {
                        // 购买数量归正
                        newTotal = currentCount - amountToReduce;
                    }
                    
                    int clampedTotal = trad.ClampAmount(newTotal);
                    if (trad.CanAdjustTo(clampedTotal).Accepted)
                    {
                        trad.AdjustTo(clampedTotal);
                        inputValues[tradeableId] = 0;
                        onChanged?.Invoke();
                    }
                }
            }
        }

        private static void DrawCurrencyPanel(Rect rect, Tradeable currency)
        {
            if (currency == null) return;

            Widgets.DrawBoxSolid(rect, new Color(0.15f, 0.15f, 0.16f, 1f));
            GUI.color = ColAccentCamo3;
            Widgets.DrawLineHorizontal(rect.x, rect.y, rect.width);
            GUI.color = Color.white;

            Rect inner = rect.ContractedBy(PANEL_PAD);
            float contentX = inner.x;
            float contentWidth = inner.width;

            float y = inner.y;

            // 货币标题行 整行可点击查看InfoCard
            Rect titleRect = new(contentX, y, contentWidth, ROW_TITLE);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Text.WordWrap = false;
            GUI.color = ColAccentCamo3;
            Widgets.Label(titleRect, currency.LabelCap);

            if (Mouse.IsOver(titleRect))
            {
                Widgets.DrawHighlight(titleRect);
                string tip = currency.LabelCap;
                if (!currency.TipDescription.NullOrEmpty())
                    tip = tip + ": " + currency.TipDescription;
                TooltipHandler.DrawInstantTooltip(tip);
                if (Widgets.ButtonInvisible(titleRect) && currency.AnyThing != null)
                    Find.WindowStack.Add(new Dialog_InfoCard(currency.AnyThing));
            }

            GUI.color = Color.white;
            y += ROW_TITLE;

            // 第一组分隔
            y += DIVIDER_PAD * 0.5f;
            GUI.color = new Color(1f, 1f, 1f, 0.1f);
            Widgets.DrawLineHorizontal(inner.x, y, inner.width);
            GUI.color = Color.white;
            y += DIVIDER_PAD * 0.5f;

            int currentTotal = currency.CountHeldBy(Transactor.Colony);
            float netBill = totalBuyValue - totalSellValue;

            // 计算实际扣款 含找零损失
            float actualSettlement = netBill > 0 ? projectedConsumableValue : netBill;
            float wastage = projectedConsumableValue - netBill;
            int afterBalance = (netBill > 0)
                ? (int)(currentTotal - projectedConsumableValue)
                : (int)(currentTotal + netBill);
            int visualChange = afterBalance - currentTotal;

            // 标签宽度计算 用最大标签对齐
            Text.Font = GameFont.Tiny;
            float buyW = Text.CalcSize("USAC.Trade.Summary.Buy".Translate()).x;
            float sellW = Text.CalcSize("USAC.Trade.Summary.Sell".Translate()).x;
            Text.Font = GameFont.Small;
            float settleW = Text.CalcSize("USAC.Trade.Summary.Settlement".Translate()).x;
            float currentW = Text.CalcSize("USAC.Trade.Summary.Current".Translate()).x;
            float afterW = Text.CalcSize("USAC.Trade.Summary.AfterRemain".Translate()).x;
            float labelWidth = Mathf.Max(Mathf.Max(buyW, sellW), Mathf.Max(settleW, Mathf.Max(currentW, afterW))) + 10f;
            float valueWidth = contentWidth - labelWidth - 5f;

            // 买入价值行
            DrawLabelValueRow(contentX, y, labelWidth, valueWidth, ROW_TINY,
                "USAC.Trade.Summary.Buy".Translate(),
                totalBuyValue.ToString("F0"),
                ColTextMuted, ColAccentCamo3,
                GameFont.Tiny, GameFont.Tiny);
            y += ROW_TINY;

            // 卖出价值行
            DrawLabelValueRow(contentX, y, labelWidth, valueWidth, ROW_TINY,
                "USAC.Trade.Summary.Sell".Translate(),
                totalSellValue.ToString("F0"),
                ColTextMuted, ColAccentCamo3,
                GameFont.Tiny, GameFont.Tiny);
            y += ROW_TINY + ROW_GAP;

            // 本次结算行 主数字
            string settleText;
            Color settleColor;
            if (Mathf.Abs(actualSettlement) < 0.5f)
            {
                settleText = "0";
                settleColor = ColAccentCamo3;
            }
            else if (actualSettlement > 0)
            {
                settleText = $"-{actualSettlement:F0}";
                settleColor = new Color(1f, 0.45f, 0.45f);
            }
            else
            {
                settleText = $"+{-actualSettlement:F0}";
                settleColor = new Color(0.55f, 0.95f, 0.55f);
            }

            DrawLabelValueRow(contentX, y, labelWidth, valueWidth, ROW_SMALL,
                "USAC.Trade.Summary.Settlement".Translate(),
                settleText,
                ColTextActive, settleColor,
                GameFont.Small, GameFont.Small);
            y += ROW_SMALL;

            // 找零损失副文本 仅有损失时显示
            if (wastage > 0.5f && netBill > 0)
            {
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleRight;
                GUI.color = new Color(0.95f, 0.75f, 0.35f);
                Text.WordWrap = false;
                Widgets.Label(
                    new Rect(contentX, y, contentWidth, ROW_SUB),
                    "USAC.Trade.Summary.IncludeWastage".Translate(wastage.ToString("F0")));
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                y += ROW_SUB;
            }

            // 第二组分隔
            y += DIVIDER_PAD * 0.5f;
            GUI.color = new Color(1f, 1f, 1f, 0.1f);
            Widgets.DrawLineHorizontal(inner.x, y, inner.width);
            GUI.color = Color.white;
            y += DIVIDER_PAD * 0.5f;

            // 当前资产行
            DrawLabelValueRow(contentX, y, labelWidth, valueWidth, ROW_TINY,
                "USAC.Trade.Summary.Current".Translate(),
                currentTotal.ToString(),
                ColTextMuted, ColTextActive,
                GameFont.Tiny, GameFont.Small);
            y += ROW_TINY + ROW_GAP;

            // 交易后剩余行 视觉终点
            Color afterColor = visualChange < 0
                ? new Color(1f, 0.45f, 0.45f)
                : (visualChange > 0 ? new Color(0.55f, 0.95f, 0.55f) : ColTextActive);
            DrawLabelValueRow(contentX, y, labelWidth, valueWidth, ROW_SMALL,
                "USAC.Trade.Summary.AfterRemain".Translate(),
                afterBalance.ToString(),
                ColTextActive, afterColor,
                GameFont.Small, GameFont.Small);
            y += ROW_SMALL;

            // 小贴士 仅在无任何交易时填充
            if (activeTradeables.Count == 0)
            {
                float tipY = y + TIP_GAP;
                Text.Font = GameFont.Tiny;
                Text.WordWrap = true;
                string tipText = $"USAC.Trade.Summary.Tip_{currentTipIndex}".Translate();
                float tipH = Text.CalcHeight(tipText, inner.width);
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = new Color(0.6f, 0.6f, 0.6f);
                Widgets.Label(new Rect(inner.x, tipY, inner.width, tipH), tipText);
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
            }

            Text.WordWrap = true;
        }

        // 绘制对齐的标签数值行
        private static void DrawLabelValueRow(float x, float y, float labelWidth, float valueWidth, float height,
            string label, string value,
            Color labelColor, Color valueColor,
            GameFont labelFont, GameFont valueFont)
        {
            Text.Font = labelFont;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = labelColor;
            Text.WordWrap = false;
            Widgets.Label(new Rect(x, y, labelWidth, height), label);

            Text.Font = valueFont;
            Text.Anchor = TextAnchor.MiddleRight;
            GUI.color = valueColor;
            Widgets.Label(new Rect(x + labelWidth, y, valueWidth, height), value);

            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }
        private static float CalculateProjectedConsumption(float netCost)
        {
            if (netCost <= 0) return 0f;
            
            // 模拟结算逻辑以计算实际消耗额
            float remaining = netCost;
            float totalConsumed = 0f;
            
            var currency = TradeSession.deal.AllTradeables.FirstOrDefault(x => x is Tradeable_USACCurrency) as Tradeable_USACCurrency;
            if (currency == null) return netCost;

            bool useBondsFirst = Tradeable_USACCurrency.EnableBondsForPayment && Tradeable_USACCurrency.UseBondsForPayment;
            
            if (useBondsFirst)
            {
                float bondsVal = GetAvailableBondsValue();
                float consumedFromBonds = Mathf.Min(Mathf.Ceil(remaining / 1000f) * 1000f, bondsVal);
                totalConsumed += consumedFromBonds;
                remaining -= consumedFromBonds;
            }

            if (remaining > 0)
            {
                // 获取所有存储单元价值
                var bags = GetColonyCorpseBagsSorted();
                foreach (float bagVal in bags)
                {
                    if (remaining <= 0) break;
                    totalConsumed += bagVal;
                    remaining -= bagVal;
                }
            }
            
            // 若仍然未结清且未选优先债券
            if (remaining > 0 && !useBondsFirst && Tradeable_USACCurrency.EnableBondsForPayment)
            {
                float bondsVal = GetAvailableBondsValue();
                float consumedFromBonds = Mathf.Min(Mathf.Ceil(remaining / 1000f) * 1000f, bondsVal);
                totalConsumed += consumedFromBonds;
            }

            return totalConsumed;
        }

        private static float GetAvailableBondsValue()
        {
            var currency = TradeSession.deal.AllTradeables.FirstOrDefault(x => x is Tradeable_USACCurrency) as Tradeable_USACCurrency;
            if (currency == null) return 0f;
            
            int count = 0;
            var things = TradeSession.deal.AllTradeables.First(x => x.IsCurrency).thingsColony;
            if (things == null) return 0f;

            foreach (var t in things)
            {
                if (t != null && t.def == USAC_DefOf.USAC_Bond) count += t.stackCount;
            }
            return count * 1000f;
        }

        private static List<float> GetColonyCorpseBagsSorted()
        {
            var list = new List<float>();
            var things = TradeSession.deal.AllTradeables.First(x => x.IsCurrency).thingsColony;
            if (things == null) return list;

            foreach (var thing in things)
            {
                if (thing is Building_USACCorpseStorage storage)
                {
                    foreach (Thing t in storage.GetDirectlyHeldThings())
                    {
                        if (t is Corpse c) list.Add(Building_CorpseBag.CalculateCorpseValue(c));
                    }
                }
                else if (thing is Building_CorpseBag bag && bag.HasCorpse)
                {
                    list.Add(Building_CorpseBag.CalculateCorpseValue(bag.ContainedCorpse));
                }
            }
            list.Sort();
            return list;
        }
        #endregion
    }
}
