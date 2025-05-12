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
            var hediffDef = Props.pawnHediff;
            if (hediffDef == null)
                return;

            // 发动技能赋予小人状态效果，再次发动时移除
            var existing = pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef);
            if (existing != null)
            {
                pawn.health.RemoveHediff(existing);
                this.parent.ResetCooldown();
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
