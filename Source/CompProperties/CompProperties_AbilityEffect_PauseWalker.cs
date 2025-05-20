using RimWorld;
using Verse;

namespace PauseWalker.CompProperties
{
    public class CompProperties_AbilityEffect_PauseWalker : CompProperties_AbilityEffect
    {
        public HediffDef? pawnHediff;
        public EffecterDef? effecterDef;
        public int maintainForTicks = -1;
        public float scale = 1f;
        public CompProperties_AbilityEffect_PauseWalker()
        {
            this.compClass = typeof(CompAbilityEffect_PauseWalker);
        }
    }
}
