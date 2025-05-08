using RimWorld;

namespace PauseWalker.Defs
{
    [DefOf]
    public static class PauseWalkAbilityDefOf
    {
        public static AbilityDef PauseWalkAbility;

        static PauseWalkAbilityDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(PauseWalkAbilityDefOf));
        }
    }
}
