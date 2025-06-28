using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace PauseWalker.CustomAbility
{
    public class PauseWalkerAbility : Ability
    {
        public PauseWalkerAbility() : base() { }
        public PauseWalkerAbility(Pawn pawn) : base(pawn) { }
        public PauseWalkerAbility(Pawn pawn, Precept sourcePrecept):base(pawn, sourcePrecept) { }
        public PauseWalkerAbility(Pawn pawn, AbilityDef def):base(pawn, def) { }
        public PauseWalkerAbility(Pawn pawn, Precept sourcePrecept, AbilityDef def) : base(pawn, sourcePrecept, def) { }
        override public bool GizmoDisabled(out string reason)
        {
            if (this.CanCooldown && this.OnCooldown && (!this.def.cooldownPerCharge || this.RemainingCharges == 0))
            {
                reason = "AbilityOnCooldown".Translate(this.CooldownTicksRemaining.ToStringTicksToPeriod(true, false, true, true, false)).Resolve();
                return true;
            }
            if (this.UsesCharges && this.RemainingCharges <= 0)
            {
                reason = "AbilityNoCharges".Translate();
                return true;
            }
            if (!this.comps.NullOrEmpty<AbilityComp>())
            {
                for (int i = 0; i < this.comps.Count; i++)
                {
                    if (this.comps[i].GizmoDisabled(out reason))
                    {
                        return true;
                    }
                }
            }
            AcceptanceReport canCast = this.CanCast;
            if (!canCast.Accepted)
            {
                reason = canCast.Reason;
                return true;
            }
            Lord lord = this.pawn.GetLord();
            if (lord != null)
            {
                AcceptanceReport report = lord.AbilityAllowed(this);
                if (!report)
                {
                    reason = report.Reason;
                    return true;
                }
            }
            if (!this.pawn.Drafted && this.def.disableGizmoWhileUndrafted && this.pawn.GetCaravan() == null && !DebugSettings.ShowDevGizmos)
            {
                reason = "AbilityDisabledUndrafted".Translate();
                return true;
            }
            if (this.pawn.DevelopmentalStage.Baby())
            {
                reason = "IsIncapped".Translate(this.pawn.LabelShort, this.pawn);
                return true;
            }
            //if (this.pawn.Downed)
            //{
            //    reason = "CommandDisabledUnconscious".TranslateWithBackup("CommandCallRoyalAidUnconscious").Formatted(this.pawn);
            //    return true;
            //}
            if (this.pawn.Deathresting)
            {
                reason = "CommandDisabledDeathresting".Translate(this.pawn);
                return true;
            }
            if (this.def.casterMustBeCapableOfViolence && this.pawn.WorkTagIsDisabled(WorkTags.Violent))
            {
                reason = "IsIncapableOfViolence".Translate(this.pawn.LabelShort, this.pawn);
                return true;
            }
            if (!this.CanQueueCast)
            {
                reason = "AbilityAlreadyQueued".Translate();
                return true;
            }
            reason = null;
            return false;
        }
    }
}
