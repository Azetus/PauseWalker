using RimWorld;

namespace PauseWalker.Defs
{
    [DefOf]
    public static class DropRoadRollerAbilityDefOf
    {
        public static AbilityDef DropRoadRollerAbility;

        static DropRoadRollerAbilityDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(DropRoadRollerAbilityDefOf));
        }
    }
}
