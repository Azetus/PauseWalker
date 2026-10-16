using PauseWalker.Utilities;
using RimWorld;
using Verse;

namespace PauseWalker.CustomAbility
{
    public class PauseWalkerSubAbility : Ability
    {
        public PauseWalkerSubAbility() { }
        public PauseWalkerSubAbility(Pawn pawn) : base(pawn) { }
        public PauseWalkerSubAbility(Pawn pawn, Precept sourcePrecept) : base(pawn, sourcePrecept) { }
        public PauseWalkerSubAbility(Pawn pawn, AbilityDef def) : base(pawn, def) { }
        public PauseWalkerSubAbility(Pawn pawn, Precept sourcePrecept, AbilityDef def) : base(pawn, sourcePrecept, def) { }

        private AcceptanceReport TimeStopAvailability
        {
            get
            {
                if (!Utils.HasPauseWalkerAbility(pawn))
                    return "PauseWalker.NotPauseWalker".Translate();

                // Reject old instances after their Hediff is removed, even if time stop is activated again.
                if (!Utils.HasPauseWalkerHediff(pawn) || Find.TickManager?.CurTimeSpeed != TimeSpeed.Paused ||
                    !pawn.abilities.AllAbilitiesForReading.Contains(this))
                    return "PauseWalker.NoPauseWalkerHediff".Translate();

                return true;
            }
        }

        public override AcceptanceReport CanCast
        {
            get
            {
                var availability = TimeStopAvailability;
                return availability.Accepted ? base.CanCast : availability;
            }
        }

        public override bool CanApplyOn(LocalTargetInfo target)
        {
            return TimeStopAvailability.Accepted && base.CanApplyOn(target);
        }

        public override bool Activate(LocalTargetInfo target, LocalTargetInfo dest)
        {
            // Verb_CastAbility calls Activate for every shot of a burst, not just the first one.
            return TimeStopAvailability.Accepted && base.Activate(target, dest);
        }
    }
}
