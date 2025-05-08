using RimWorld;
using Verse;

namespace PauseWalker.CompProperties
{

    public class CompAbilityEffect_PauseWalk : CompAbilityEffect
    {
        public new CompProperties_AbilityEffect_PauseWalk Props
        {
            get
            {
                return (CompProperties_AbilityEffect_PauseWalk)this.props;
            }
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn pawn = parent.pawn;
            var hediffDef = Props.pawnHediff;
            if (hediffDef == null)
                return;

            var existing = pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef);
            if (existing != null)
            {
                pawn.health.RemoveHediff(existing);
            }
            else
            {
                pawn.health.AddHediff(HediffMaker.MakeHediff(hediffDef, pawn));
                if (Find.TickManager.CurTimeSpeed != TimeSpeed.Paused)
                    Find.TickManager.TogglePaused();
            }
        }
    }
}
