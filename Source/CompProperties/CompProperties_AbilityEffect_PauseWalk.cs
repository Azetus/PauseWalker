using RimWorld;
using Verse;

namespace PauseWalker.CompProperties
{
    public class CompProperties_AbilityEffect_PauseWalk : CompProperties_AbilityEffect
    {
        public HediffDef? pawnHediff;

        public CompProperties_AbilityEffect_PauseWalk()
        {
            this.compClass = typeof(CompAbilityEffect_PauseWalk);
        }
    }
}
