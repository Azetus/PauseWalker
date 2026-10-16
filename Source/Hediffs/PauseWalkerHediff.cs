using Verse;
using Verse.AI;

namespace PauseWalker.Hediffs
{
    public class PauseWalkerHediff : HediffWithComps
    {
        public override void PostRemoved()
        {
            base.PostRemoved();

            var abilities = AllAbilitiesForReading;
            if (abilities.NullOrEmpty() || pawn.jobs == null)
                return;

            // Removed temporary abilities no longer receive AbilityTick; stop their pending casts explicitly.
            pawn.jobs.jobQueue.RemoveAll(pawn, job => job.ability != null && abilities.Contains(job.ability));
            foreach (var ability in abilities)
                ability.verb.Reset();

            if (pawn.CurJob?.ability is { } currentAbility && abilities.Contains(currentAbility))
                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
        }
    }
}
