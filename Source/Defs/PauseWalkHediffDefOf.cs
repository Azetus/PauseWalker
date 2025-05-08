using RimWorld;
using Verse;

namespace PauseWalker.Defs
{
    [DefOf]
    public static class PauseWalkHediffDefOf
    {
        public static HediffDef PauseWalkHediff;

        static PauseWalkHediffDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(PauseWalkHediffDefOf));
        }
    }
}
