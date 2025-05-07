using HarmonyLib;
using PauseWalker.Utilities;
using Verse;

namespace PauseWalker.Patches
{
    /// <summary>
    /// 游戏很多地方都用了 GenTicks.TicksGame 获取游戏当前时间（本质是包装了一下 TickManager.ticksGameInt 字段的get方法）。
    /// NOTE: 还有很多地方直接用了 TickManager.ticksGameInt 字段的get方法，没细看有什么区别。不知道是否也应该patch一下返回值
    /// 
    /// 用Prefix来patch一下这个方法，在游戏暂停时修改返回值，返回mod里的模拟时间，确保小人的工作能够正常生成
    /// NOTE: 暂未测试是否会导致预料外的行为
    /// </summary>

    [HarmonyPatch(typeof(GenTicks), nameof(GenTicks.TicksGame), MethodType.Getter)]
    public static class GenTicksTicksGamePatch
    {
        public static bool Prefix(ref int __result)
        {
            if (Find.TickManager.CurTimeSpeed == TimeSpeed.Paused)
            {
                __result = SimulatedTickManager.SimulatedTicksGameInt;
                return false;
            }

            return true;
        }
    }
}
