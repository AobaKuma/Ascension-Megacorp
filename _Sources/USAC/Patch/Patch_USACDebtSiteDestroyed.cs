using HarmonyLib;
using RimWorld.Planet;
using USAC.Endings;
using Verse;

namespace USAC.Patch
{
    // 检测USAC债务据点被玩家摧毁
    [HarmonyPatch(typeof(Site), nameof(Site.Destroy))]
    public static class Patch_USACDebtSiteDestroyed
    {
        [HarmonyPostfix]
        public static void Postfix(Site __instance)
        {
            // 仅处理USAC债务据点
            if (__instance.def != USAC_DefOf.USAC_DebtSite) return;

            var debtComp = GameComponent_USACDebt.Instance;
            if (debtComp == null) return;

            // 优先关联处于据点模式的合同
            DebtContract contract = null;
            if (debtComp.ActiveContracts != null)
            {
                for (int i = 0; i < debtComp.ActiveContracts.Count; i++)
                {
                    var c = debtComp.ActiveContracts[i];
                    if (c.IsActive && c.IsInSiteMode)
                    {
                        contract = c;
                        break;
                    }
                }
                // 降级回退取第一个活跃合同
                if (contract == null)
                {
                    for (int i = 0; i < debtComp.ActiveContracts.Count; i++)
                    {
                        if (debtComp.ActiveContracts[i].IsActive)
                        {
                            contract = debtComp.ActiveContracts[i];
                            break;
                        }
                    }
                }
            }

            // 通知结局管理器
            USACEndingManager.NotifyDebtSiteDestroyed(contract);
        }
    }
}
