﻿using HarmonyLib;
using PauseWalker.Utilities;
using Verse;

namespace PauseWalker.Patches.TicksGamePatch
{
    [HarmonyPatch(typeof(TickManager), nameof(TickManager.TicksGame), MethodType.Getter)]
    public static class TickManagerTicksGamePatch
    {
        public static bool Prefix(ref int __result)
        {

            if (Current.Game == null || Find.TickManager == null)
            {
                return true;
            }

            try
            {
                if (Find.TickManager.CurTimeSpeed == TimeSpeed.Paused)
                {
                    if (Utils.CurrentMapContainsPauseWalker(Find.CurrentMap))
                    {
                        __result = SimulatedTickManager.SimulatedTicksGameInt;
                        return false;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warning($"[PauseWalker] Exception in TickManagerTicksGamePatch: {e}");
                return true;
            }
            return true;
        }
    }
}
