using PauseWalker.Defs;
using PauseWalker.Utilities;
using RimWorld;
using Verse;

namespace PauseWalker.CompProperties
{

    public class CompAbilityEffect_PauseWalker : CompAbilityEffect
    {
        public new CompProperties_AbilityEffect_PauseWalker Props
        {
            get
            {
                return (CompProperties_AbilityEffect_PauseWalker)this.props;
            }
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn pawn = parent.pawn;
            HediffDef? pauseWalkerHediffDef = Props.pawnHediff;
            if (pauseWalkerHediffDef == null)
                return;
            ToggleHediffState(pawn, pauseWalkerHediffDef);
            GainNewAbility(pawn);
        }

        private void ToggleHediffState(Pawn pawn, HediffDef pauseWalkerHediffDef) {
            // 发动技能赋予小人状态效果，再次发动时移除
            var existing = pawn.health.hediffSet.GetFirstHediffOfDef(pauseWalkerHediffDef);
            if (existing != null)
            {
                pawn.health.RemoveHediff(existing);
                this.parent.ResetCooldown();
            }
            else
            {
                pawn.health.AddHediff(HediffMaker.MakeHediff(pauseWalkerHediffDef, pawn));
                if (Find.TickManager.CurTimeSpeed != TimeSpeed.Paused)
                    Find.TickManager.TogglePaused();
            }
        }

        private void GainNewAbility(Pawn pawn) {
            Pawn_AbilityTracker? pawnAbilityTracker = pawn.abilities;
            if (pawnAbilityTracker == null) return;

            bool hasHediff = PauseWalkerUtils.HasPauseWalkerHediff(pawn);
            bool hasAbility = pawnAbilityTracker.GetAbility(DropRoadRollerAbilityDefOf.DropRoadRollerAbility) != null;

            if (!hasAbility && hasHediff)
            {
                pawnAbilityTracker.GainAbility(DropRoadRollerAbilityDefOf.DropRoadRollerAbility);
            }
            if (hasAbility && !hasHediff) {
                pawnAbilityTracker.RemoveAbility(DropRoadRollerAbilityDefOf.DropRoadRollerAbility);
            }
        }
    }
}
