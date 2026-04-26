using RimWorld;
using Verse;

namespace PauseWalker.Defs
{
    [DefOf]
    public static class PauseWalkerHediffDefOf
    {
        public static HediffDef PauseWalkerHediff;

        static PauseWalkerHediffDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(PauseWalkerHediffDefOf));
        }
    }
}
