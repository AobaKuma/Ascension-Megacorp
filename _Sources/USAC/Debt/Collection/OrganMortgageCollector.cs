using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace USAC
{
    // 摘取目标非致命器官
    // 不够还则拆碎(杀死并销毁尸体)
    public class OrganMortgageCollector : ICollectionStrategy
    {
        public float Execute(Map map, float targetAmount,
            DebtContract contract)
        {
            if (map == null) return 0f;

            float collected = 0f;
            var pawns = GetHarvestCandidates(map);

            foreach (var pawn in pawns)
            {
                if (collected >= targetAmount) break;

                // 先尝试摘取不致死器官
                float organValue = HarvestOrgans(pawn, map);
                collected += organValue;

                // 若器官不够还 则直接拆碎
                if (collected < targetAmount)
                {
                    collected += DemolishPawn(pawn, map);
                }
            }

            return collected;
        }

        // 获取可摘取的殖民者候选
        private List<Pawn> GetHarvestCandidates(Map map)
        {
            return map.mapPawns.AllPawnsSpawned
                .Where(p => p.IsColonist && !p.Dead
                    && !p.RaceProps.IsMechanoid)
                .OrderBy(p => p.MarketValue)
                .ToList();
        }

        // 摘取所有不致死器官
        private float HarvestOrgans(Pawn pawn, Map map)
        {
            float totalValue = 0f;
            var parts = GetHarvestable(pawn);

            foreach (var part in parts)
            {
                if (pawn.Dead) break;

                var spawned = MedicalRecipesUtility
                    .SpawnNaturalPartIfClean(
                        pawn, part, pawn.Position, map);

                if (spawned != null)
                {
                    totalValue += spawned.MarketValue;
                    // 对部位造成手术伤害来移除
                    pawn.TakeDamage(new DamageInfo(
                        DamageDefOf.SurgicalCut, 99999f,
                        999f, -1f, null, part));
                }
            }

            if (totalValue > 0)
            {
                Messages.Message(
                    $"[USAC] 器官抵押执行：已摘取{pawn.LabelShort}" +
                    $"的器官，价值₿{totalValue:F0}",
                    pawn, MessageTypeDefOf.ThreatBig);
            }

            return totalValue;
        }

        // 获取可安全摘取的身体部位
        private List<BodyPartRecord> GetHarvestable(Pawn pawn)
        {
            var result = new List<BodyPartRecord>();
            var parts = pawn.health.hediffSet
                .GetNotMissingParts();

            // 按配对分组 只取每对中的一个
            var paired = new HashSet<string>();

            foreach (var part in parts)
            {
                if (part.def.spawnThingOnRemoved == null)
                    continue;
                if (!MedicalRecipesUtility
                    .IsCleanAndDroppable(pawn, part))
                    continue;

                // 排除致命部位
                if (IsVitalSolePart(pawn, part))
                    continue;

                // 配对器官只取一个
                string pairKey = part.def.defName;
                if (part.def.tags != null
                    && part.def.tags.Any(
                        t => t == BodyPartTagDefOf.Mirrored))
                {
                    if (paired.Contains(pairKey))
                        continue;
                    paired.Add(pairKey);
                }

                result.Add(part);
            }

            return result;
        }

        // 判断是否为致命的唯一部位
        private bool IsVitalSolePart(Pawn pawn, BodyPartRecord part)
        {
            // 心脏 大脑 肝脏 脊柱等
            if (part.def.tags == null) return false;

            bool isVital =
                part.def.tags.Contains(
                    BodyPartTagDefOf.ConsciousnessSource)
                || part.def.tags.Contains(
                    BodyPartTagDefOf.BloodPumpingSource);

            if (!isVital) return false;

            // 检查是否为唯一存活的该类型部位
            var siblings = pawn.health.hediffSet
                .GetNotMissingParts()
                .Where(p => p.def == part.def)
                .ToList();

            return siblings.Count <= 1;
        }

        // 杀死目标收缴器官
        private float DemolishPawn(Pawn pawn, Map map)
        {
            float value = pawn.MarketValue;

            Messages.Message(
                $"[USAC] 债务清算执行：{pawn.LabelShort}已被完全拆解",
                MessageTypeDefOf.ThreatBig);

            pawn.Kill(null);
            // 销毁尸体(器官和皮肉全被收缴)
            if (pawn.Corpse is { Spawned: true })
            {
                pawn.Corpse.Destroy();
            }

            return value;
        }
    }
}
