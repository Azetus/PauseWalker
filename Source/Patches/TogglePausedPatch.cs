using HarmonyLib;
using PauseWalker.Utilities;
using Verse;

namespace PauseWalker.Patches
{
    // 用 Postfix 监听一下 TogglePaused 是否触发，处理 SimulatedTickManager
    [HarmonyPatch(typeof(TickManager), nameof(TickManager.TogglePaused))]
    public static class TogglePausedPatch
    {
        static void Postfix(TickManager __instance)
        {
            TimeSpeed currentTimeSpeed = __instance.CurTimeSpeed;
            // 如果游戏刚刚进入暂停状态, 初始化模拟的 TicksGameInt
            if (currentTimeSpeed == TimeSpeed.Paused)
            {
                SimulatedTickManager.InitSimTick();
            }
            // 如果游戏恢复到非暂停状态, 清理模拟的 TicksGameInt
            else
            {
                SimulatedTickManager.ClearSimTick();
            }
        }
    }
}
