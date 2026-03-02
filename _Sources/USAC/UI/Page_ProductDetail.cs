using UnityEngine;
using Verse;
using RimWorld;
using static USAC.InternalUI.PortalUIUtility;

namespace USAC.InternalUI
{
    // USAC 产品详情展示页面
    public class Page_ProductDetail : IPortalPage
    {
        public string Title => "USAC.UI.Detail.Title".Translate();

        public void Draw(Rect rect, Dialog_USACPortal parent)
        {
            // 过渡中获取活跃参数
            string defName = parent.GetParamActive("def");
            if (defName == null) { parent.NavigateTo("usac://internal/products"); return; }

            USACProductDef product = DefDatabase<USACProductDef>.GetNamed(defName, false);
            if (product == null) { Widgets.Label(rect, "USAC.UI.Error.ProductNotFound".Translate()); return; }

            var anim = parent.Animator;

            // 检查共享元素动画
            bool isSharedTrans = anim.IsPlaying && anim.Kind == PortalAnimator.TransitionKind.SharedElement;

            GUI.BeginGroup(rect);
            Rect localRect = new(0, 0, rect.width, rect.height);

            if (isSharedTrans)
            {
                DrawWithSharedElementAnim(localRect, product, parent, anim);
            }
            else
            {
                DrawDetailLayout(localRect, product, parent, 1f);
            }

            GUI.EndGroup();
        }

        private void DrawWithSharedElementAnim(Rect localRect, USACProductDef product, Dialog_USACPortal parent, PortalAnimator anim)
        {
            float progress = anim.Progress;

            // 建立物理坐标系
            float leftPanelWidth = 420;
            Rect detBento = new(0, 80, leftPanelWidth, 400);
            Rect detImg = new(detBento.x + 20, detBento.y + 20, detBento.width - 40, detBento.height - 40);

            // 动态追踪卡片位置
            Rect listBento = (anim.SharedElementTarget.width > 10f) ? anim.SharedElementTarget : anim.SharedElementStart;
            Rect listImg = new(listBento.x + 15, listBento.y + 15, 100, 130);

            // 计算动画起终点
            Rect startB = anim.IsBack ? detBento : anim.SharedElementStart;
            Rect endB = anim.IsBack ? listBento : detBento;

            Rect startI = anim.IsBack ? detImg : listImg;
            Rect endI = anim.IsBack ? listImg : detImg;

            // 进度曲线与分相控制
            float curvedT = progress * progress * (3f - 2f * progress);

            // UI面板分相动画
            float tUi = anim.IsBack ? Mathf.Clamp01(1f - progress / 0.4f) : Mathf.Clamp01((progress - 0.5f) / 0.5f);
            float curvedUi = tUi * tUi * (3f - 2f * tUi);

            // 执行插值绘制
            Rect currentBento = new(
                Mathf.Lerp(startB.x, endB.x, curvedT),
                Mathf.Lerp(startB.y, endB.y, curvedT),
                Mathf.Lerp(startB.width, endB.width, curvedT),
                Mathf.Lerp(startB.height, endB.height, curvedT)
            );

            Rect currentImage = new(
                Mathf.Lerp(startI.x, endI.x, curvedT),
                Mathf.Lerp(startI.y, endI.y, curvedT),
                Mathf.Lerp(startI.width, endI.width, curvedT),
                Mathf.Lerp(startI.height, endI.height, curvedT)
            );

            // 卡片文字淡入淡出
            float textAlp = anim.IsBack ? Mathf.Clamp01(progress / 0.7f) : Mathf.Clamp01(1f - progress / 0.4f);

            DrawBentoBox(currentBento, (boxRect) =>
            {
                DrawProductPreview(currentImage, product);

                if (textAlp > 0.01f)
                {
                    Rect textR = new(boxRect.x + 125, boxRect.y + 15, boxRect.width - 140, boxRect.height - 30);
                    GUI.color = ColAccentCamo1.ToTransp(textAlp);
                    Widgets.Label(textR.TopPartPixels(25), product.label);
                    GUI.color = ColTextMuted.ToTransp(textAlp);
                    Widgets.Label(new Rect(textR.x, textR.y + 25, textR.width, 20), product.subLabel);
                    GUI.color = ColTextActive.ToTransp(textAlp);
                    Text.Font = GameFont.Small;
                    Widgets.Label(new Rect(textR.x, textR.y + 50, textR.width, 80), product.CachedDescription);
                    GUI.color = Color.white;
                }
            }, false);

            if (curvedUi > 0.01f)
            {
                DrawDetailLayout(localRect, product, parent, curvedUi);
            }
        }

        private void DrawDetailLayout(Rect localRect, USACProductDef product, Dialog_USACPortal parent, float animAlpha = 1f)
        {
            float topOffset = (1f - animAlpha) * -30f;

            // 绘制回退与标题栏
            Rect header = localRect.TopPartPixels(50);
            header.y += topOffset;
            if (DrawTacticalButton(header.LeftPartPixels(100), "USAC.UI.Back".Translate()))
            {
                parent.NavigateBack();
            }

            Rect titleRect = header;
            titleRect.x += 120;
            DrawColoredLabel(titleRect, product.label.ToUpper(), ColAccentCamo1, GameFont.Medium, TextAnchor.MiddleLeft);

            // 分割线
            Widgets.DrawLineHorizontal(0, 55 + topOffset, localRect.width);

            // 左右区域划分
            float leftPanelWidth = 420;
            Rect leftSide = new(0, 80, leftPanelWidth, 400);
            Rect rightSide = new(leftPanelWidth + 30, 80, localRect.width - leftPanelWidth - 30, localRect.height - 80);

            // 左侧预览仅静态时绘制
            if (animAlpha >= 1f)
            {
                DrawEntityPreview(leftSide, product);
            }

            // 右侧规格列表
            DrawProductSpecifications(rightSide, product, animAlpha);
        }

        private void DrawEntityPreview(Rect rect, USACProductDef product)
        {
            DrawBentoBox(rect, (boxRect) =>
            {
                DrawProductPreview(boxRect.ContractedBy(20), product);
            }, false);
        }

        private void DrawProductSpecifications(Rect rect, USACProductDef product, float animAlpha = 1f)
        {
            float curY = 0;
            var orderItemDef = product.thingDef;
            var mechOrderExt = orderItemDef?.GetModExtension<ModExtension_MechOrder>();
            ThingDef mechRace = mechOrderExt?.mechKindDef?.race;

            int rowIndex = 0;
            // 逐行错开动画参数
            const float RowStep = 0.05f;
            const float RowDuration = 0.35f;

            void DrawRow(string key, string value)
            {
                float rowDelay = rowIndex * RowStep;
                float rawLocalAlpha = Mathf.Clamp01((animAlpha - rowDelay) / RowDuration);
                float rowAlpha = rawLocalAlpha * rawLocalAlpha * (3f - 2f * rawLocalAlpha);

                if (rowAlpha > 0.01f)
                {
                    float offsetX = (1f - rowAlpha) * 80f;
                    Rect r = new(rect.x + offsetX, rect.y + curY, rect.width, 32);
                    Widgets.DrawBoxSolid(r, new Color(1, 1, 1, rowAlpha * 0.03f));

                    Color c1 = new(ColTextMuted.r, ColTextMuted.g, ColTextMuted.b, rowAlpha);
                    DrawColoredLabel(new Rect(r.x + 10, r.y + 6, 160, 24), key, c1, GameFont.Tiny);

                    Color c2 = new(ColAccentCamo3.r, ColAccentCamo3.g, ColAccentCamo3.b, rowAlpha);
                    DrawColoredLabel(new Rect(r.x + 180, r.y + 6, r.width - 190, 24), value, c2, GameFont.Small);
                }

                curY += 36;
                rowIndex++;
            }

            // 基础信息
            string identifier = (product.category == "MECH" && mechRace != null) ? mechRace.defName : orderItemDef?.defName ?? product.defName;
            DrawRow("USAC.UI.Stat.Identifier".Translate(), identifier);
            DrawRow("USAC.UI.Stat.Category".Translate(), product.category.ToUpper());
            DrawRow("USAC.UI.Stat.SubType".Translate(), product.subLabel);

            // 机兵专属数据
            if (product.category == "MECH" && mechRace != null)
            {
                float move = StatUtility.GetStatValueFromList(mechRace.statBases, StatDefOf.MoveSpeed, 0);
                if (move > 0) DrawRow("USAC.UI.Stat.MoveSpeed".Translate(), move.ToString("F1") + " C/S");

                float armorS = StatUtility.GetStatValueFromList(mechRace.statBases, StatDefOf.ArmorRating_Sharp, 0);
                if (armorS > 0) DrawRow("USAC.UI.Stat.ArmorSharp".Translate(), (armorS * 100f).ToString("F0") + "%");

                float health = mechRace.race?.baseHealthScale ?? 1f;
                DrawRow("USAC.UI.Stat.HealthFact".Translate(), health.ToString("F1") + "x");

                string weight = mechRace.race?.mechWeightClass.ToString();
                if (!weight.NullOrEmpty()) DrawRow("USAC.UI.Stat.WeightClass".Translate(), weight.ToUpper());
            }

            // 武器专属数据
            if (product.category == "WEAPON" && orderItemDef != null)
            {
                var verb = (orderItemDef.Verbs != null && orderItemDef.Verbs.Count > 0) ? orderItemDef.Verbs[0] : null;
                if (verb != null)
                {
                    if (verb.defaultProjectile != null)
                    {
                        DrawRow("USAC.UI.Stat.Damage".Translate(), verb.defaultProjectile.projectile.GetDamageAmount(orderItemDef, null).ToString());
                        DrawRow("USAC.UI.Stat.ArmorPen".Translate(), (verb.defaultProjectile.projectile.GetArmorPenetration(null, null) * 100f).ToString("F0") + "%");
                    }
                    DrawRow("USAC.UI.Stat.Range".Translate(), verb.range.ToString("F0") + " CEL");
                    DrawRow("USAC.UI.Stat.Cooldown".Translate(), orderItemDef.GetStatValueAbstract(StatDefOf.RangedWeapon_Cooldown).ToString("F2") + " S");
                }
            }

            // 装备专属数据
            if (product.category == "APPAREL" && orderItemDef != null)
            {
                float sharp = orderItemDef.GetStatValueAbstract(StatDefOf.ArmorRating_Sharp);
                float blunt = orderItemDef.GetStatValueAbstract(StatDefOf.ArmorRating_Blunt);
                if (sharp > 0) DrawRow("USAC.UI.Stat.ArmorSharp".Translate(), (sharp * 100f).ToString("F0") + "%");
                if (blunt > 0) DrawRow("USAC.UI.Stat.ArmorBlunt".Translate(), (blunt * 100f).ToString("F0") + "%");

                float cold = orderItemDef.GetStatValueAbstract(StatDefOf.Insulation_Cold);
                float heat = orderItemDef.GetStatValueAbstract(StatDefOf.Insulation_Heat);
                if (cold != 0) DrawRow("USAC.UI.Stat.InsulCold".Translate(), cold.ToString("F1") + "°C");
                if (heat != 0) DrawRow("USAC.UI.Stat.InsulHeat".Translate(), heat.ToString("F1") + "°C");
            }

            // 补给数据
            if (product.category == "SUPPLY" && orderItemDef != null)
            {
                if (orderItemDef.IsIngestible)
                    DrawRow("USAC.UI.Stat.Nutrition".Translate(), orderItemDef.ingestible.CachedNutrition.ToString("F2"));
                float potency = orderItemDef.GetStatValueAbstract(StatDefOf.MedicalPotency);
                if (potency > 0) DrawRow("USAC.UI.Stat.MedPotency".Translate(), (potency * 100f).ToString("F0") + "%");
            }

            if (orderItemDef != null)
            {
                float mass = StatUtility.GetStatValueFromList(orderItemDef.statBases, StatDefOf.Mass, 0);
                DrawRow("USAC.UI.Stat.Mass".Translate(), mass.ToString("F2") + " KG");
            }

            // 摘要区域
            curY += 20;
            float descDelay = (rowIndex + 1) * RowStep;
            float rawDescAlpha = Mathf.Clamp01((animAlpha - descDelay) / RowDuration);
            float descCurved = rawDescAlpha * rawDescAlpha * (3f - 2f * rawDescAlpha);

            if (descCurved > 0.01f)
            {
                float descOffsetX = (1f - descCurved) * 80f;
                GUI.color = new Color(ColAccentCamo2.r, ColAccentCamo2.g, ColAccentCamo2.b, descCurved);
                DrawColoredLabel(new Rect(rect.x + descOffsetX, rect.y + curY, rect.width, 30), "USAC.UI.Detail.Summary".Translate(), GUI.color, GameFont.Small);
                curY += 35;

                string formattedDesc = FixCjkLineBreak(product.description);
                float descWidth = rect.width - 10;
                float descHeight = Text.CalcHeight(formattedDesc, descWidth);

                GUI.color = new Color(ColTextActive.r, ColTextActive.g, ColTextActive.b, descCurved);
                DrawColoredLabel(new Rect(rect.x + descOffsetX, rect.y + curY, descWidth, descHeight), formattedDesc, GUI.color);
            }

            GUI.color = Color.white;
        }
    }
}
