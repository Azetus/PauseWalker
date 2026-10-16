using HarmonyLib;
using PauseWalker.Utilities;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace PauseWalker.Patches
{
    // Rimworld 在发生战斗时，游戏倍速上限会被限制在一倍速，需要处理一下限制时间的时间戳，防止时停恢复之后长时间被锁定在一倍速。
    
    [HarmonyPatch(typeof(TimeSlower), nameof(TimeSlower.SignalForceNormalSpeed))]
    public static class TimeSlower_SignalForceNormalSpeed_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(TimeSlower __instance)
        {
            if (Current.Game != null && Find.TickManager != null && Find.CurrentMap != null)
            {
                if (Find.TickManager.CurTimeSpeed == TimeSpeed.Paused && WorldRendererUtility.DrawingMap && Utils.CurrentMapContainsPauseWalker(Find.CurrentMap))
                {
                    // 保持普通信号直接重设截止时间的语义，仅将时间来源替换为原始 tick。
                    __instance.forceNormalSpeedUntil = Utils.GetRawTicksGameInt() + 800;
                    // 原方法会使用模拟 TicksGame 再次写入，必须跳过以免覆盖修正。
                    return false;
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

            if (Current.Game != null && Find.TickManager != null && Find.CurrentMap != null)
            {
                if (Find.TickManager.CurTimeSpeed == TimeSpeed.Paused && WorldRendererUtility.DrawingMap && Utils.CurrentMapContainsPauseWalker(Find.CurrentMap))
                {
                    // 与普通信号使用同一时间基准，短信号不能缩短已有的长限制。
                    __instance.forceNormalSpeedUntil = Mathf.Max(__instance.forceNormalSpeedUntil, Utils.GetRawTicksGameInt() + 240);
                    // 原方法会使用模拟 TicksGame 再次写入，必须跳过以免覆盖修正。
                    return false;
                }
            }

            return true;
        }
    }
}
