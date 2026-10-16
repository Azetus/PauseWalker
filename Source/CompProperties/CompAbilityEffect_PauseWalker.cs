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
            ToggleEffecter(pawn, target);
            ToggleHediffState(pawn, pauseWalkerHediffDef);
        }

        private void ToggleHediffState(Pawn pawn, HediffDef pauseWalkerHediffDef)
        {
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

        private void ToggleEffecter(Pawn pawn, LocalTargetInfo target)
        {
            if (Utils.HasPauseWalkerHediff(pawn) || this.Props.effecterDef == null)
                return;

            Effecter effecter = ((!target.HasThing) ? this.Props.effecterDef.Spawn(target.Cell, this.parent.pawn.Map, this.Props.scale) : Props.effecterDef.Spawn(target.Thing, this.parent.pawn.Map, this.Props.scale));

            if (this.Props.maintainForTicks > 0)
            {
                this.parent.AddEffecterToMaintain(effecter, target.Cell, this.Props.maintainForTicks);
            }
            else
            {
                effecter.Cleanup();
            }
        }
    }
}
