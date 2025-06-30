using RimWorld;
using Verse;

namespace PauseWalker.Defs
{
    [DefOf]
    public class PauseWalkerResurrectHediffDefOf
    {
        public static HediffDef PauseWalkerResurrectHediff;

        static PauseWalkerResurrectHediffDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(PauseWalkerResurrectHediffDefOf));
        }
    }
}
