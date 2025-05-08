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


        public static bool IsPauseWalkerPawn(Pawn pawn)
        {
            if(pawn == null) return false;

            //return pawn.RaceProps.Humanlike &&
            //        pawn.story?.traits?.HasTrait(PauseWalkerTraitDefOf.PauseWalker) == true &&
            //        pawn.Spawned &&
            //        !pawn.Downed &&
            //        !pawn.Dead;

            //return IsPawnPauseWalk(pawn);

            return pawn.health.hediffSet.HasHediff(PauseWalkerHediffDefOf.PauseWalkerHediff);



            // return false;
        }


        public static float GetModifiedTickRate(Pawn pawn)
        {
            if (IsPauseWalkerPawn(pawn))
            {
                return 1f;
            }
            return Find.TickManager.TickRateMultiplier;
        }
    }
}
