using RimWorld;

namespace PauseWalker.Defs
{
    [DefOf]
    public class PauseWalker_ThrowKnifeAbilityDefOf
    {
        public static AbilityDef PauseWalker_ThrowKnife;

        static PauseWalker_ThrowKnifeAbilityDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(PauseWalker_ThrowKnifeAbilityDefOf));
        }
    }
}
