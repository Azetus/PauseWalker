using RimWorld;
using Verse;

namespace PauseWalker.CompProperties
{
    public class CompProperties_AbilityEffect_PauseWalker : CompProperties_AbilityEffect
    {
        public HediffDef? pawnHediff;

        public CompProperties_AbilityEffect_PauseWalker()
        {
            this.compClass = typeof(CompAbilityEffect_PauseWalker);
        }
    }
}
