using HarmonyLib;
using PauseWalker.ModGameComponent;
using PauseWalker.Utilities;
using Verse;

namespace PauseWalker.Patches.TimeSpeedPatch
{
    [HarmonyPatch(typeof(TickManager), nameof(TickManager.CurTimeSpeed), MethodType.Setter)]
    public static class SetCurTimeSpeedPatch
    {
        public static void Postfix(TickManager __instance)
        {
            TimeSpeed currentTimeSpeed = __instance.CurTimeSpeed;
            if (currentTimeSpeed == TimeSpeed.Paused)
            {
                // 如果游戏刚刚进入暂停状态, 初始化模拟的 TicksGameInt
                SimulatedTickManager.InitSimTick();
            }
            else
            {
                // 如果游戏恢复到非暂停状态, 清理模拟的 TicksGameInt
                SimulatedTickManager.ClearSimTick();
                PauseWalkerManager.Instance?.ClearAllPauseWalkerHediffs();
            }
        }
    }
}
