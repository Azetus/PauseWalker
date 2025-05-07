using RimWorld;

namespace PauseWalker.Defs
{
    [DefOf]
    public static class PauseWalkerTraitDefOf
    {
        public static TraitDef PauseWalker;

        static PauseWalkerTraitDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(PauseWalkerTraitDefOf));
        }
    }
}
