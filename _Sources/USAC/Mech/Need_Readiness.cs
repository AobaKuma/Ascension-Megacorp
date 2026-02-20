using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace USAC
{
    // 定义机兵整备需求逻辑类
    public class Need_Readiness : Need
    {
        public Need_Readiness(Pawn pawn) : base(pawn)
        {
            threshPercents = new System.Collections.Generic.List<float> { 0.01f };
        }

        private CompMechReadiness Comp => pawn.TryGetComp<CompMechReadiness>();

        public override float MaxLevel => Comp?.Props.capacity ?? 100f;

        public override int GUIChangeArrow => -1;

        public override float CurLevel
        {
            get => Comp?.Readiness ?? curLevelInt;
            set
            {
                float clamped = UnityEngine.Mathf.Clamp(value, 0f, MaxLevel);
                curLevelInt = clamped;
                Comp?.SetReadinessDirectly(clamped);
            }
        }

        // 判定整备需求列表可见性
        public override bool ShowOnNeedList => Comp != null && pawn.Faction != null && pawn.Faction.IsPlayer;

        public override void NeedInterval()
        {
            // 依赖组件时钟
        }

        public override string GetTipString()
        {
            StringBuilder sb = new StringBuilder(base.GetTipString());
            var comp = Comp;
            if (comp != null)
            {
                float percent = comp.Props.consumptionPerDay / comp.Props.capacity * 100f;
                sb.AppendInNewLine("USAC_ReadinessConsumption".Translate(percent.ToString("F1")));
            }
            return sb.ToString();
        }
    }
}
