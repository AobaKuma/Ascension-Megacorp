using System.Linq;
using RimWorld;
using Verse;

namespace USAC
{
    // 初始化机兵相关定义
    // 自动同步机兵属性至残骸
    [StaticConstructorOnStartup]
    public static class USACMechStatInitializer
    {
        static USACMechStatInitializer()
        {
            InitializeMechStats();
        }

        private static void InitializeMechStats()
        {
            // 检索指定交易标签机兵定义
            var mechDefs = DefDatabase<ThingDef>.AllDefs
                .Where(d => d.race != null && d.tradeTags != null && d.tradeTags.Contains("USAC_Mech"))
                .ToList();

            foreach (var mechDef in mechDefs)
            {
                // 执行机兵物理重量计算
                float mechMass = CalculateMechMass(mechDef);

                // 执行机兵对应残骸重量设置
                var wreckComp = mechDef.GetCompProperties<CompProperties_MechWreck>();
                if (wreckComp?.wreckDef != null)
                {
                    SetMass(wreckComp.wreckDef, mechMass);
                }
            }
        }

        private static float CalculateMechMass(ThingDef mechDef)
        {
            // 基于体型数值计算重量
            float bodySize = mechDef.race?.baseBodySize ?? 1f;

            // 根据体型计算重量
            return 30f + bodySize * 20f;
        }

        private static void SetMass(ThingDef def, float mass)
        {
            if (def == null) return;

            // 校验属性基础列表存续性
            if (def.statBases == null)
            {
                def.statBases = new System.Collections.Generic.List<StatModifier>();
            }

            // 检索现有重量属性定义
            var existingMass = def.statBases.FirstOrDefault(s => s.stat == StatDefOf.Mass);

            if (existingMass != null)
            {
                // 仅在默认值时执行数值覆盖
                if (existingMass.value == 50f)
                {
                    existingMass.value = mass;
                }
            }
            else
            {
                def.statBases.Add(new StatModifier { stat = StatDefOf.Mass, value = mass });
            }
        }
    }
}
