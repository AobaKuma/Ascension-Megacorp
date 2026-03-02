using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
using static USAC.InternalUI.PortalUIUtility;

namespace USAC.InternalUI
{
    // USAC 资产终端页面
    public class Page_Assets : IPortalPage
    {
        public string Title => "USAC.UI.Assets.Title".Translate();

        private Vector2 scrollPos;
        private float curApplyAmount = -1f;
        private float curRepayAmount = 1000f;

        // 手动定价参数缓存
        private float _targetInterest = 0.08f;
        private float _targetGrowth = 0.05f;
        private DebtGrowthMode _targetGrowthMode = DebtGrowthMode.PrincipalBased;

        public void Draw(Rect rect, Dialog_USACPortal parent)
        {
            var comp = GameComponent_USACDebt.Instance;
            if (comp == null) return;

            string view = parent.GetParam("view") ?? "index";

            if (view == "index") DrawIndex(rect, parent, comp);
            else if (view == "contract") DrawContractDetail(rect, parent, comp);
        }

        #region 索引与申请合成视图
        private void DrawIndex(Rect rect, Dialog_USACPortal parent, GameComponent_USACDebt comp)
        {
            // 顶栏汇总
            Rect headerRect = new(0, 0, rect.width, 80);
            DrawUIGradient(headerRect, ColHeaderBg, ColWindowBg);
            Widgets.DrawBoxSolidWithOutline(headerRect, Color.clear, ColBorder);
            DrawColoredLabel(headerRect.LeftPartPixels(200).ContractedBy(15),
                "USAC.UI.Assets.CreditScore".Translate(comp.CreditScore), ColAccentCamo1, GameFont.Medium);
            DrawColoredLabel(new Rect(220, 15, 350, 50),
                "USAC.UI.Assets.TotalDebt".Translate(comp.TotalDebt.ToString("N0")), ColAccentRed, GameFont.Medium);

            Rect scrollRect = new(0, 100, rect.width, rect.height - 100);
            // 计算动态视图高度
            float viewH = Mathf.Max(scrollRect.height, comp.ActiveContracts.Count * 110 + 550);
            Rect viewRect = new(0, 0, rect.width - 20, viewH);

            Widgets.BeginScrollView(scrollRect, ref scrollPos, viewRect);
            float y = 0;

            // 绘制活动合同列表
            DrawColoredLabel(new Rect(0, y, rect.width, 30),
                "USAC.UI.Assets.ActiveContracts".Translate(), ColAccentCamo2, GameFont.Small);
            y += 35;

            if (comp.ActiveContracts.Count == 0)
            {
                GUI.color = ColTextMuted;
                Widgets.Label(new Rect(0, y, rect.width, 30), "USAC.UI.Assets.NoAgreement".Translate());
                y += 40;
            }
            else
            {
                foreach (var contract in comp.ActiveContracts)
                    y = DrawContractRow(y, rect.width, contract, parent);
            }

            y += 30;

            // 绘制贷款申请面板
            DrawLoanApplicationPanel(ref y, rect.width - 20, comp);

            Widgets.EndScrollView();
        }

        private float DrawContractRow(float y, float width, DebtContract contract, Dialog_USACPortal parent)
        {
            Rect r = new(0, y, width - 20, 90);
            bool danger = contract.MissedPayments >= 2;
            Color bg = danger ? new Color(0.3f, 0.05f, 0.05f, 1f) : ColHeaderBg;
            Widgets.DrawBoxSolidWithOutline(r, bg, ColBorder);

            Rect inner = r.ContractedBy(12);
            DrawColoredLabel(inner.TopPartPixels(22), contract.Label, ColAccentCamo1);

            string statusLine = $"₿{contract.Principal:N0}  |  " +
                $"{"USAC.UI.Assets.Interest".Translate()}: ₿{contract.AccruedInterest:N0}  |  " +
                $"{"USAC.UI.Assets.NextSettlement".Translate()}: {GameComponent_USACDebt.GetTimeToNextCycle(contract)}";
            DrawColoredLabel(new Rect(inner.x, inner.y + 26, inner.width - 110, 20),
                statusLine, ColTextMuted, GameFont.Tiny);

            if (danger)
            {
                DrawColoredLabel(new Rect(inner.x, inner.y + 46, inner.width - 110, 20),
                    $"⚠ {"USAC.UI.Assets.MissedWarning".Translate(contract.MissedPayments)}", ColAccentRed, GameFont.Tiny);
            }

            if (DrawTacticalButton(new Rect(r.xMax - 100, r.y + 25, 85, 40),
                "USAC.UI.Detail.Btn".Translate(), key: $"contract_detail_{contract.ContractId}"))
                parent.NavigateTo($"usac://internal/assets?view=contract&id={contract.ContractId}");

            return y + 105;
        }

        private void DrawLoanApplicationPanel(ref float y, float width, GameComponent_USACDebt comp)
        {
            Rect panelRect = new(0, y, width, 480);
            Widgets.DrawBoxSolidWithOutline(panelRect, new Color(0, 0, 0, 0.2f), ColBorder);
            Rect inner = panelRect.ContractedBy(20);

            DrawColoredLabel(new Rect(inner.x, inner.y, inner.width, 30),
                "USAC.UI.Assets.Apply.Title".Translate(), ColAccentCamo1, GameFont.Medium);

            float curY = inner.y + 40;

            // --- 风险定价调节区 ---
            // 绘制周期利率调节
            DrawParamAdjuster(ref curY, inner.width, "USAC.UI.Assets.InterestRate".Translate(),
                ref _targetInterest, 0.01f, 0.01f, 0.50f, "{0:P0}", "interest");

            // 绘制手续费调节
            DrawParamAdjuster(ref curY, inner.width, "USAC.UI.Assets.GrowthRate".Translate(),
                ref _targetGrowth, 0.01f, 0f, 0.30f, "{0:P0}", "growth");

            // 绘制增长模式切换
            float modeLabelY = curY;
            DrawColoredLabel(new Rect(inner.x, modeLabelY + 3, 160, 30), "USAC.UI.Assets.Stat.GrowthMode".Translate(), ColTextMuted, GameFont.Tiny, TextAnchor.MiddleLeft);
            string modeLabel = _targetGrowthMode == DebtGrowthMode.WealthBased
                ? "USAC.UI.Assets.Stat.Wealth".Translate()
                : "USAC.UI.Assets.Stat.Principal".Translate();

            if (DrawTacticalButton(new Rect(inner.x + 165, modeLabelY, 140, 32), modeLabel, key: "apply_growth_mode"))
            {
                _targetGrowthMode = _targetGrowthMode == DebtGrowthMode.WealthBased
                    ? DebtGrowthMode.PrincipalBased : DebtGrowthMode.WealthBased;
            }
            curY += 48;

            // --- 实时评估显示 ---
            var eval = comp.EvaluateLoan(_targetInterest, _targetGrowth, _targetGrowthMode);

            Rect evalCard = new(inner.x, curY, inner.width, 100);
            DrawBentoBox(evalCard, (box) =>
            {
                Rect bInner = box.ContractedBy(15);
                float col = bInner.width / 3f;
                DrawStatCell(bInner.x, bInner.y, col, "USAC.UI.Assets.Stat.MaxAmount".Translate(), $"₿{eval.MaxAmount:N0}", ColAccentCamo3);
                DrawStatCell(bInner.x + col, bInner.y, col, "USAC.UI.Assets.Stat.ActualInterest".Translate(), $"{eval.InterestRate:P1}");
                DrawStatCell(bInner.x + col * 2, bInner.y, col, "USAC.UI.Assets.Stat.CreditDiscount".Translate(), $"-{eval.CreditDiscount:P0}", ColAccentCamo3);
            }, false);
            curY += 115;

            // --- 金额调节器 ---
            float maxLoanK = Mathf.Floor(eval.MaxAmount / 1000f) * 1000f;
            if (curApplyAmount < 0 || curApplyAmount > maxLoanK) curApplyAmount = Mathf.Max(0, maxLoanK);

            Rect adjBox = new(inner.x, curY, inner.width, 64);
            Widgets.DrawBoxSolidWithOutline(adjBox, ColHeaderBg, ColBorder);

            if (!eval.IsAvailable)
            {
                DrawColoredLabel(adjBox, eval.BlockReason, ColAccentRed, GameFont.Medium, TextAnchor.MiddleCenter);
            }
            else
            {
                DrawColoredLabel(new Rect(adjBox.x + 115, adjBox.y, adjBox.width - 230, 64),
                    $"₿{curApplyAmount:N0}", Color.white, GameFont.Medium, TextAnchor.MiddleCenter);

                // 金额加减按钮对齐
                float btnY = adjBox.y + 12;
                if (DrawTacticalButton(new Rect(adjBox.x + 15, btnY, 45, 40), "-10k", curApplyAmount >= 11000f, key: "apply_m10k"))
                    curApplyAmount -= 10000f;
                if (DrawTacticalButton(new Rect(adjBox.x + 65, btnY, 40, 40), "-1k", curApplyAmount >= 2000f, key: "apply_m1k"))
                    curApplyAmount -= 1000f;

                if (DrawTacticalButton(new Rect(adjBox.xMax - 105, btnY, 40, 40), "+1k", curApplyAmount + 1000f <= maxLoanK, key: "apply_p1k"))
                    curApplyAmount += 1000f;
                if (DrawTacticalButton(new Rect(adjBox.xMax - 60, btnY, 45, 40), "+10k", curApplyAmount + 10000f <= maxLoanK, key: "apply_p10k"))
                    curApplyAmount += 10000f;
            }
            curY += 80;

            // --- 签署按钮 ---
            if (DrawTacticalButton(new Rect(inner.xMax - 160, panelRect.yMax - 60, 150, 45),
                "USAC.UI.Assets.Apply.Confirm".Translate(), eval.IsAvailable && curApplyAmount >= 1000f, key: "apply_confirm"))
            {
                comp.ApplyForLoan(DebtType.WholeMortgage, curApplyAmount, eval.GrowthRate, eval.InterestRate, eval.GrowthMode);
                Messages.Message("USAC_LoanApproved".Translate(curApplyAmount.ToString("N0")), MessageTypeDefOf.PositiveEvent);
                curApplyAmount = -1; // 重置
            }

            y += 500;
        }

        private void DrawParamAdjuster(ref float y, float width, string label, ref float value, float step, float min, float max, string format, string keyPrefix)
        {
            // 文本标签垂直对齐按钮中心
            DrawColoredLabel(new Rect(0, y + 3, 160, 30), label, ColTextMuted, GameFont.Tiny, TextAnchor.MiddleLeft);

            // 紧凑、平衡的操作按钮组
            float btnX = 165;
            if (DrawTacticalButton(new Rect(btnX, y, 32, 32), "-", key: $"{keyPrefix}_minus"))
                value = Mathf.Max(min, value - step);

            DrawColoredLabel(new Rect(btnX + 35, y, 70, 32), string.Format(format, value), Color.white, GameFont.Small, TextAnchor.MiddleCenter);

            if (DrawTacticalButton(new Rect(btnX + 108, y, 32, 32), "+", key: $"{keyPrefix}_plus"))
                value = Mathf.Min(max, value + step);

            y += 42;
        }
        #endregion

        #region 合同详情视图
        private void DrawContractDetail(Rect rect, Dialog_USACPortal parent, GameComponent_USACDebt comp)
        {
            string id = parent.GetParam("id");
            var contract = comp.ActiveContracts.FirstOrDefault(c => c.ContractId == id);
            if (contract == null)
            { parent.NavigateTo("usac://internal/assets?view=index"); return; }

            DrawContractHeader(rect, contract, parent);

            float y = 60;
            DrawFinancialStatusCard(ref y, rect.width, contract);
            DrawWarningBanner(ref y, rect.width, contract);
            DrawInterestPayPanel(ref y, rect.width, contract, comp);
            DrawPrincipalRepayPanel(ref y, rect.width, contract, comp);
            DrawGrowthForecastCard(ref y, rect.width, contract);
        }

        private void DrawContractHeader(Rect rect, DebtContract contract, Dialog_USACPortal parent)
        {
            if (DrawTacticalButton(new Rect(0, 0, 80, 30), "USAC.UI.Back".Translate(), key: "back_to_index"))
                parent.NavigateTo("usac://internal/assets?view=index");

            DrawColoredLabel(new Rect(100, 0, rect.width - 300, 30),
                contract.Label.ToUpper(), ColAccentCamo1, GameFont.Medium, TextAnchor.MiddleLeft);
            DrawColoredLabel(new Rect(rect.width - 200, 0, 195, 30),
                $"ID: {contract.ContractId}", ColTextMuted, GameFont.Tiny, TextAnchor.MiddleRight);

            Widgets.DrawLineHorizontal(0, 35, rect.width);
        }

        private void DrawFinancialStatusCard(ref float y, float width, DebtContract contract)
        {
            Rect r = new(0, y, width, 100);
            DrawBentoBox(r, (box) =>
            {
                Rect inner = box.ContractedBy(15);
                float col = inner.width / 4f;

                DrawStatCell(inner.x, inner.y, col, "USAC.UI.Assets.Principal".Translate(), $"₿{contract.Principal:N0}");
                DrawStatCell(inner.x + col, inner.y, col, "USAC.UI.Assets.Interest".Translate(), $"₿{contract.AccruedInterest:N0}",
                    contract.AccruedInterest > 0 ? ColAccentRed : ColAccentCamo3);
                DrawStatCell(inner.x + col * 2, inner.y, col, "USAC.UI.Assets.Stat.InterestRate".Translate(), $"{contract.InterestRate * 100:F1}%");
                DrawStatCell(inner.x + col * 3, inner.y, col, "USAC.UI.Assets.NextSettlement".Translate(),
                    GameComponent_USACDebt.GetTimeToNextCycle(contract),
                    contract.MissedPayments > 0 ? ColAccentRed : ColAccentCamo3);
            }, false);
            y += 110;
        }

        private void DrawStatCell(float x, float y, float w, string label, string value, Color? valueColor = null)
        {
            DrawColoredLabel(new Rect(x + 5, y + 2, w - 10, 20), label, ColTextMuted, GameFont.Tiny);
            DrawColoredLabel(new Rect(x + 5, y + 26, w - 10, 30), value, valueColor ?? ColAccentCamo3, GameFont.Medium);
        }

        private void DrawWarningBanner(ref float y, float width, DebtContract contract)
        {
            if (contract.MissedPayments <= 0) return;

            bool critical = contract.MissedPayments >= 2;
            Color bg = critical ? new Color(0.45f, 0.04f, 0.04f, 1f) : new Color(0.35f, 0.2f, 0.0f, 1f);
            Rect r = new(0, y, width, 50);
            Widgets.DrawBoxSolidWithOutline(r, bg, ColAccentRed.ToTransp(0.6f));

            string icon = critical ? "⚠⚠" : "⚠";
            string msg = critical
                ? "USAC.UI.Assets.Warning.Critical".Translate(contract.MissedPayments)
                : "USAC.UI.Assets.Warning.Caution".Translate(contract.MissedPayments);
            DrawColoredLabel(new Rect(15, y + 8, width - 30, 34),
                $"{icon}  {msg}", ColAccentRed, GameFont.Small, TextAnchor.MiddleLeft);
            y += 58;
        }

        private void DrawInterestPayPanel(ref float y, float width, DebtContract contract, GameComponent_USACDebt comp)
        {
            Rect r = new(0, y, width, 80);
            DrawBentoBox(r, (box) =>
            {
                Rect inner = box.ContractedBy(15);
                int bondsNeeded = Mathf.CeilToInt(contract.AccruedInterest / 1000f);
                int bondsOwned = comp.GetBondCountOnMap();

                DrawColoredLabel(inner.TopPartPixels(20), "USAC.UI.Assets.InterestPanel.Title".Translate(), ColTextMuted, GameFont.Tiny);

                if (contract.AccruedInterest <= 0)
                {
                    DrawColoredLabel(new Rect(inner.x, inner.y + 22, inner.width, 24), "USAC.UI.Assets.InterestPaid".Translate(), ColAccentCamo3);
                }
                else
                {
                    string interestLine = $"₿{contract.AccruedInterest:N0}  ({"USAC.UI.Assets.BondsNeeded".Translate(bondsNeeded)})";
                    DrawColoredLabel(new Rect(inner.x, inner.y + 22, inner.width - 180, 24), interestLine, ColAccentRed);
                    DrawColoredLabel(new Rect(inner.x, inner.y + 44, 300, 18), "USAC.UI.Assets.BondsOwned".Translate(bondsOwned), ColTextMuted, GameFont.Tiny);

                    if (DrawTacticalButton(new Rect(inner.xMax - 170, inner.y + 10, 165, 40),
                        "USAC.UI.Assets.PayInterest".Translate(bondsNeeded), bondsOwned >= bondsNeeded, key: $"pay_interest_{contract.ContractId}"))
                    {
                        if (contract.TryPayInterest(Find.AnyPlayerHomeMap))
                            Messages.Message("USAC_PaymentSuccess".Translate(contract.Label), MessageTypeDefOf.PositiveEvent);
                    }
                }
            }, false);
            y += 88;
        }

        private void DrawPrincipalRepayPanel(ref float y, float width, DebtContract contract, GameComponent_USACDebt comp)
        {
            Rect r = new(0, y, width, 140);
            DrawBentoBox(r, (box) =>
            {
                Rect inner = box.ContractedBy(15);
                DrawColoredLabel(inner.TopPartPixels(20), "USAC.UI.Assets.RepayPanel.Title".Translate(), ColTextMuted, GameFont.Tiny);

                float freeLimit = contract.Principal * 0.10f;
                float used = contract.PrincipalPaidThisQuarter;
                string feeHint = $"{"USAC.UI.Assets.QuarterFree".Translate()} ₿{freeLimit:N0} | {"USAC.UI.Assets.Used".Translate()} ₿{used:N0} | 10%▸免费 20%▸50% 30%▸100% 50%+▸200%";
                DrawColoredLabel(new Rect(inner.x, inner.y + 22, inner.width, 18), feeHint, ColTextMuted, GameFont.Tiny);

                Rect adjRow = new(inner.x, inner.y + 44, inner.width - 185, 32);
                float maxRepayK = Mathf.Floor(contract.Principal / 1000f) * 1000f;
                curRepayAmount = Mathf.Min(curRepayAmount, maxRepayK);
                if (DrawTacticalButton(adjRow.LeftPartPixels(50), "-10k", curRepayAmount > 1000f, key: "repay_m10k")) curRepayAmount = Mathf.Max(1000f, curRepayAmount - 10000f);
                if (DrawTacticalButton(new Rect(adjRow.x + 55, adjRow.y, 45, 32), "-1k", curRepayAmount > 1000f, key: "repay_m1k")) curRepayAmount = Mathf.Max(1000f, curRepayAmount - 1000f);
                DrawColoredLabel(new Rect(adjRow.x + 110, adjRow.y, adjRow.width - 165, 32), $"₿{curRepayAmount:N0}", Color.white, GameFont.Medium, TextAnchor.MiddleCenter);
                if (DrawTacticalButton(new Rect(adjRow.xMax - 100, adjRow.y, 45, 32), "+1k", curRepayAmount + 1000f <= maxRepayK, key: "repay_p1k")) curRepayAmount += 1000f;
                if (DrawTacticalButton(adjRow.RightPartPixels(50), "+10k", curRepayAmount + 10000f <= maxRepayK, key: "repay_p10k")) curRepayAmount += 10000f;

                float totalThisQ = used + curRepayAmount;
                float fee = SurchargeTable.Calculate(contract.Principal, totalThisQ) - SurchargeTable.Calculate(contract.Principal, used);
                int totalBonds = Mathf.CeilToInt((curRepayAmount + fee) / 1000f);
                bool canRepay = contract.AccruedInterest <= 0 && comp.GetBondCountOnMap() >= totalBonds;

                string btnLabel = fee > 0 ? "USAC.UI.Assets.RepaySurcharge".Translate(totalBonds, Mathf.CeilToInt(fee / 1000f)) : "USAC.UI.Assets.Repay".Translate(totalBonds);
                if (DrawTacticalButton(new Rect(inner.xMax - 180, inner.y + 44, 175, 64), btnLabel, canRepay, key: $"repay_confirm_{contract.ContractId}"))
                {
                    string err = contract.TryPayPrincipal(Find.AnyPlayerHomeMap, Mathf.CeilToInt(curRepayAmount / 1000f));
                    if (err != null) Messages.Message(err, MessageTypeDefOf.RejectInput);
                }
            }, false);
            y += 148;
        }

        private void DrawGrowthForecastCard(ref float y, float width, DebtContract contract)
        {
            if (contract.GrowthRate <= 0f) return;
            float predicted = GameComponent_USACDebt.PredictNextGrowth(contract);
            Rect r = new(0, y, width, 80);
            DrawBentoBox(r, (box) =>
            {
                Rect bInner = box.ContractedBy(15);
                DrawColoredLabel(bInner.TopPartPixels(20), "USAC.UI.Assets.GrowthForecast".Translate(), ColTextMuted, GameFont.Tiny);
                string modeKey = "USAC.UI.Assets.GrowthMode." + contract.GrowthMode;
                DrawColoredLabel(new Rect(bInner.x, bInner.y + 22, 250, 20), modeKey.Translate((int)(contract.GrowthRate * 100)), ColTextMuted, GameFont.Tiny);
                DrawColoredLabel(new Rect(bInner.xMax - 150, bInner.y + 20, 140, 30), $"+₿{predicted:N0}", ColAccentRed, GameFont.Medium, TextAnchor.MiddleRight);
            }, false);
            y += 88;
        }
        #endregion
    }
}
