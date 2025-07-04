using RimWorld;
using Verse;

namespace PauseWalker.Defs
{
    [DefOf]
    public class PauseWalkerReturnEffecterDefOf
    {
        public static EffecterDef PauseWalker_Return;

        static PauseWalkerReturnEffecterDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(PauseWalkerReturnEffecterDefOf));
        }

    }
}
