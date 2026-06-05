using RimWorld;
using Verse;


namespace PauseWalker.Defs
{
    [DefOf]
    public class PauseWalkerReturnLetterDefOf
    {
        public static LetterDef PauseWalkerReturnLetter;

        static PauseWalkerReturnLetterDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(PauseWalkerReturnLetterDefOf));
        }
    }
}
