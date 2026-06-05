using HarmonyLib;
using PauseWalker.Utilities;
using UnityEngine;
using Verse;

namespace PauseWalker.Patches
{
    [HarmonyPatch(typeof(TimeSlower), nameof(TimeSlower.SignalForceNormalSpeed))]
    public static class TimeSlower_SignalForceNormalSpeed_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(TimeSlower __instance)
        {
            if (Find.TickManager != null && Find.CurrentMap != null)
            {
                if (Find.TickManager.CurTimeSpeed == TimeSpeed.Paused && Utils.CurrentMapContainsPauseWalker(Find.CurrentMap))
                {
                    if (AccessTools.Field(__instance.GetType(), "forceNormalSpeedUntil") is { } forceNormalSpeedUntilField &&
                        forceNormalSpeedUntilField.GetValue(__instance) is int)
                    {
                        forceNormalSpeedUntilField.SetValue(__instance, Mathf.Max(Utils.GetRawTicksGameInt() + 800));
                    }

                }
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(TimeSlower), nameof(TimeSlower.SignalForceNormalSpeedShort))]
    public static class TimeSlower_SignalForceNormalSpeedShort_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(TimeSlower __instance)
        {

            if (Find.TickManager != null && Find.CurrentMap != null)
            {
                if (Find.TickManager.CurTimeSpeed == TimeSpeed.Paused && Utils.CurrentMapContainsPauseWalker(Find.CurrentMap))
                {
                    if (AccessTools.Field(__instance.GetType(), "forceNormalSpeedUntil") is { } forceNormalSpeedUntilField &&
                        forceNormalSpeedUntilField.GetValue(__instance) is int forceNormalSpeedUntil)
                    {
                        forceNormalSpeedUntilField.SetValue(__instance, Mathf.Max(forceNormalSpeedUntil, Utils.GetRawTicksGameInt() + 240));
                    }
                }
            }

            return true;
        }
    }
}
