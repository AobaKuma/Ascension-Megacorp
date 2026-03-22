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
            
            Rect valueRect = new(rect.x, rect.y + 40, rect.width, 80);
            DrawValueSummary(valueRect);
            
            Rect listRect = new(rect.x, rect.y + 125, rect.width, rect.height - 275);
            DrawTradeList(listRect, onChanged);
            
            Rect footerRect = new(rect.x, rect.yMax - 145, rect.width, 145);
            DrawCurrencyFooter(footerRect, currency);
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

        private static void DrawValueSummary(Rect rect)
        {
            Rect inner = rect.ContractedBy(5);
            
            float netCost = totalBuyValue - totalSellValue;
            
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            
            float labelWidth = 60f;
            float valueWidth = inner.width - labelWidth - 5;
            
            // 购买总额
            GUI.color = ColTextMuted;
            Widgets.Label(new Rect(inner.x, inner.y, labelWidth, 20), "USAC.Trade.Summary.Buy".Translate());
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleRight;
            GUI.color = ColAccentCamo3;
            Widgets.Label(new Rect(inner.x + labelWidth, inner.y, valueWidth, 20), totalBuyValue.ToString("F0"));
            
            // 出售总额
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = ColTextMuted;
            Widgets.Label(new Rect(inner.x, inner.y + 25, labelWidth, 20), "USAC.Trade.Summary.Sell".Translate());
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleRight;
            GUI.color = ColAccentCamo3;
            Widgets.Label(new Rect(inner.x + labelWidth, inner.y + 25, valueWidth, 20), totalSellValue.ToString("F0"));
            
            // 净支出
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = ColTextMuted;
            Widgets.Label(new Rect(inner.x, inner.y + 50, labelWidth, 20), "USAC.Trade.Summary.Net".Translate());
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleRight;
            GUI.color = netCost > 0 ? new Color(1f, 0.4f, 0.4f) : ColAccentCamo3;
            string netText = netCost > 0 ? $"-{netCost:F0}" : $"+{-netCost:F0}";
            Widgets.Label(new Rect(inner.x + labelWidth, inner.y + 50, valueWidth, 20), netText);
            
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private static void DrawTradeList(Rect rect, System.Action onChanged)
        {
            Rect inner = rect.ContractedBy(5);
            float viewHeight = activeTradeables.Count * 45f + 10f;
            Rect viewRect = new(0, 0, inner.width - 20f, viewHeight);
            
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
            
            // 调整器
            float adjustWidth = 80f;
            float adjustX = inner.xMax - adjustWidth;
            Rect adjustRect = new(adjustX, inner.y + 4, adjustWidth, 28);
            DrawReduceAdjuster(adjustRect, trad, isSelling, onChanged);
            
            Text.Anchor = TextAnchor.UpperLeft;
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

        private static void DrawCurrencyFooter(Rect rect, Tradeable currency)
        {
            if (currency == null) return;
            
            Widgets.DrawBoxSolid(rect, new Color(0.15f, 0.15f, 0.16f, 1f));
            GUI.color = ColAccentCamo3;
            Widgets.DrawLineHorizontal(rect.x, rect.y, rect.width);
            GUI.color = Color.white;
            
            Rect inner = rect.ContractedBy(12);
            
            // 图标
            if (currency.AnyThing != null)
            {
                Rect iconRect = new(inner.x, inner.y + 10, 50, 50);
                Widgets.ThingIcon(iconRect, currency.AnyThing);
                
                if (Mouse.IsOver(iconRect))
                {
                    TooltipHandler.TipRegionByKey(iconRect, "DefInfoTip");
                    if (Widgets.ButtonInvisible(iconRect))
                        Find.WindowStack.Add(new Dialog_InfoCard(currency.AnyThing));
                }
            }
            // 货币名称
            float contentX = inner.x + 60;
            float contentWidth = inner.width - 65;
            
            // 输出货币名称
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = ColAccentCamo3;
            Text.WordWrap = false;
            
            Rect labelRect = new(contentX, inner.y, contentWidth, 22);
            Widgets.Label(labelRect, currency.Label);
            
            if (Mouse.IsOver(labelRect))
            {
                Widgets.DrawHighlight(labelRect);
                TooltipHandler.TipRegion(labelRect, () =>
                {
                    string text = currency.LabelCap;
                    string tipDescription = currency.TipDescription;
                    if (!tipDescription.NullOrEmpty())
                        text = text + ": " + tipDescription;
                    return text;
                }, currency.GetHashCode());
            }
            
            // 获取当前资产总量
            int currentTotal = currency.CountHeldBy(Transactor.Colony);
            float netBill = totalBuyValue - totalSellValue;
            
            // 预计结算后剩余资产
            int afterBalance = (netBill > 0) ? (int)(currentTotal - projectedConsumableValue) : (int)(currentTotal + netBill);
            int visualChange = afterBalance - currentTotal;
            
            float row2Y = inner.y + 28;
            Text.Font = GameFont.Tiny;
            GUI.color = ColTextMuted;
            Text.Anchor = TextAnchor.MiddleLeft;
            Text.WordWrap = false;
            
            // 资产汇总行 避开侧边图标说明
            float labelWidth = Text.CalcSize("USAC.Trade.Summary.Current".Translate()).x + 5;
            Widgets.Label(new Rect(contentX, row2Y, labelWidth, 20), "USAC.Trade.Summary.Current".Translate());
            
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            Widgets.Label(new Rect(contentX + labelWidth, row2Y, contentWidth - labelWidth, 20), currentTotal.ToString());
            
            // 实际变动提示
            if (visualChange != 0)
            {
                float row3Y = inner.y + 52;
                Text.Font = GameFont.Tiny;
                GUI.color = ColTextMuted;
                Text.Anchor = TextAnchor.MiddleLeft;
                Text.WordWrap = false;
                
                float afterLabelWidth = Text.CalcSize("USAC.Trade.Summary.After".Translate()).x + 5;
                Widgets.Label(new Rect(contentX, row3Y, afterLabelWidth, 20), "USAC.Trade.Summary.After".Translate());
                
                Text.Font = GameFont.Small;
                GUI.color = visualChange < 0 ? ColAccentRed : ColAccentCamo3;
                
                string afterText = afterBalance.ToString();
                float afterValueWidth = Text.CalcSize(afterText).x + 10;
                Widgets.Label(new Rect(contentX + afterLabelWidth, row3Y, afterValueWidth, 20), afterText);
                
                // 标注支出对资产的影响
                Text.Font = GameFont.Tiny;
                GUI.color = ColTextMuted;
                string changeText = visualChange > 0 ? $"(+{visualChange})" : $"({visualChange})";
                Widgets.Label(new Rect(contentX + afterLabelWidth + afterValueWidth, row3Y, contentWidth - afterLabelWidth - afterValueWidth, 20), changeText);
            }
            else
            {
                float row3Y = inner.y + 52;
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.5f, 0.5f, 0.5f);
                Text.Anchor = TextAnchor.MiddleLeft;
                Text.WordWrap = false;
                Widgets.Label(new Rect(contentX, row3Y, contentWidth, 20), "USAC.Trade.Summary.NoChange".Translate());
            }
            
            // 底部全宽区域绘制说明一线说明
            float row4Y = inner.y + 80;
            GUI.color = new Color(1f, 1f, 1f, 0.1f);
            Widgets.DrawLineHorizontal(inner.x, row4Y, inner.width);
            
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.6f, 0.6f, 0.6f);
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.WordWrap = true;
            
            float wastage = projectedConsumableValue - (totalBuyValue - totalSellValue);
            string footerText = (visualChange < 0 && wastage > 0.01f)
                ? "USAC.Trade.Summary.Wastage".Translate(wastage.ToString("F0"))
                : $"USAC.Trade.Summary.Tip_{currentTipIndex}".Translate();
                
            Rect footerTextRect = new(inner.x, row4Y + 5, inner.width, 50);
            Widgets.Label(footerTextRect, footerText);
            
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.WordWrap = true;
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
