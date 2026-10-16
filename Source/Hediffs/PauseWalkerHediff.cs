using Verse;
using Verse.AI;

namespace PauseWalker.Hediffs
{
    public class PauseWalkerHediff : HediffWithComps
    {
        // 由原版健康更新移除非暂停状态下的残留标记，并继续执行 PostRemoved 清理。
        // 保留父类及 HediffComp 的移除条件；恢复时间时的统一清理仍负责立即结束时停。
        public override bool ShouldRemove =>
            base.ShouldRemove ||
            (Current.Game != null && Find.TickManager != null &&
             Find.TickManager.CurTimeSpeed != TimeSpeed.Paused);

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
