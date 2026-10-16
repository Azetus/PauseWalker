using HarmonyLib;
using PauseWalker.Utilities;
using RimWorld.Planet;
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
                if (Find.TickManager.CurTimeSpeed == TimeSpeed.Paused && WorldRendererUtility.DrawingMap && Utils.CurrentMapContainsPauseWalker(Find.CurrentMap))
                {
                    __instance.forceNormalSpeedUntil = Mathf.Max(Utils.GetRawTicksGameInt() + 800);

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
                if (Find.TickManager.CurTimeSpeed == TimeSpeed.Paused && WorldRendererUtility.DrawingMap && Utils.CurrentMapContainsPauseWalker(Find.CurrentMap))
                {
                    __instance.forceNormalSpeedUntil = Mathf.Max(__instance.forceNormalSpeedUntil, Utils.GetRawTicksGameInt() + 240);
                }
            }

            return true;
        }
    }
}
