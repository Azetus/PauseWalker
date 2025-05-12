using RimWorld;

namespace PauseWalker.Defs
{
    [DefOf]
    public static class PauseWalkerAbilityDefOf
    {
        public static AbilityDef PauseWalkerAbility;

        static PauseWalkerAbilityDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(PauseWalkerAbilityDefOf));
        }
    }
}
