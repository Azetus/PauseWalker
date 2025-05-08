using HarmonyLib;
using PauseWalker.Defs;
using Verse;

namespace PauseWalker.Utilities
{
    public static class PauseWalkerUtils
    {
        public static int GetRawTicksGameInt()
        {
            if (Current.Game != null && Find.TickManager != null)
            {
                var rawTicksGame = AccessTools.Field(Find.TickManager.GetType(), "ticksGameInt").GetValue(Find.TickManager);
                if (rawTicksGame is int ticks)
                {
                    return ticks;
                }
            }

            return 0;
        }

        // 用小人身上的状态效果(Hediff)判断该小人能否在暂停时移动
        public static bool IsPauseWalkerPawn(Pawn pawn)
        {
            if(pawn == null) return false;

            return pawn.health.hediffSet.HasHediff(PauseWalkHediffDefOf.PauseWalkHediff);

        }



    }
}
