using HarmonyLib;
using PauseWalker.Defs;
using PauseWalker.Utilities;
using Verse;

namespace PauseWalker.Patches
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

                var currentMap = Find.CurrentMap;
                if (currentMap != null)
                {
                    List<Pawn> targetPawn = currentMap.mapPawns.AllPawnsSpawned.ToList();
                    foreach (var pawn in targetPawn)
                    {
                        // 游戏恢复到非暂停状态时清空所有pawn的特殊状态PauseWalkHediff
                        var existing = pawn.health.hediffSet.GetFirstHediffOfDef(PauseWalkHediffDefOf.PauseWalkHediff);
                        if (existing != null)
                        {
                            pawn.health.RemoveHediff(existing);
                        }
                    }
                }


            }
        }
    }
}
