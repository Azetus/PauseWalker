using HarmonyLib;
using PauseWalker.Utilities;
using Verse;

namespace PauseWalker.Patches
{
    [HarmonyPatch(typeof(DamageFlasher), nameof(DamageFlasher.Notify_DamageApplied))]
    public static class DamageFlasherPatch
    {
        public static bool Prefix(DamageFlasher __instance, DamageInfo dinfo)
        {
            // 处理一下时停状态下受击变红不恢复的问题
            if (Find.TickManager != null && Find.CurrentMap != null)
            {

                if (Find.TickManager.CurTimeSpeed == TimeSpeed.Paused && Utils.CurrentMapContainsPauseWalker(Find.CurrentMap))
                {
                    if (dinfo.Def.harmsHealth)
                    {
                        if (AccessTools.Field(__instance.GetType(), "lastDamageTick") is { } lastDamageTickField &&
                            lastDamageTickField.GetValue(__instance) is int)
                        {
                            lastDamageTickField.SetValue(__instance, Utils.GetRawTicksGameInt());
                            return false;
                        }
                    }
                }

            }



            return true;
        }
    }
}
