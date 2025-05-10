using HarmonyLib;
using PauseWalker.Defs;
using Verse;
namespace PauseWalker.Utilities
{
    internal static class PauseWalkerUtils
    {
        public static int GetRawTicksGameInt()
        {
            if (Current.Game != null && Find.TickManager != null)
            {
                if(AccessTools.Field(Find.TickManager.GetType(), "ticksGameInt") is { } rawTicksGameField &&
                    rawTicksGameField.GetValue(Find.TickManager) is int ticks)
                {
                    return ticks;
                }
            }

            return 0;
        }

        // 用小人身上的状态效果(Hediff)判断该小人能否在暂停时移动
        public static bool IsPauseWalkerPawn(Pawn? pawn)
        {
            if(pawn == null) return false;

            return pawn.health.hediffSet.HasHediff(PauseWalkHediffDefOf.PauseWalkHediff);

        }

        public static bool CurrentMapContainsPauseWalker(Map currentMap)
        {
            if(Current.Game != null && Find.TickManager != null && currentMap != null && currentMap.mapPawns != null)
            {
                var spawnedPawns = currentMap.mapPawns.AllPawnsSpawned;
                if (spawnedPawns.Count > 0) {
                    return spawnedPawns.Any(pawn =>
                    {
                        return IsPauseWalkerPawn(pawn);
                    });
                }
            }

            return false;
        }

    }
}
