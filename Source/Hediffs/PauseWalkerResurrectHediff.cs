using RimWorld;
using Verse;

namespace PauseWalker.Hediffs
{
    public class PauseWalkerResurrectHediff : HediffWithComps
    {
        public override bool Visible
        {
            get
            {
                return false;
            }
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            if (this.pawn.Dead && this.PlayerControlled)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Revive",
                    defaultDesc = "revive",
                    icon = PauseWalkerResurrectHediff.Icon.Texture,
                    action = () =>
                    {
                        this.Use();
                        Messages.Message("PawnRevived".Translate(this.pawn), this.pawn, MessageTypeDefOf.PositiveEvent);
                    }
                };
            }
            yield break;
        }

        private void Use()
        {
            Messages.Message("MessageUsingSelfResurrection".Translate(this.pawn), this.pawn, MessageTypeDefOf.NeutralEvent, true);
            ResurrectionUtility.TryResurrect(this.pawn);
        }

        public bool PlayerControlled
        {
            get
            {
                return this.pawn.IsColonist && (this.pawn.HostFaction == null || this.pawn.IsSlave);
            }
        }
        private static readonly CachedTexture Icon = new CachedTexture("UI/Abilities/SelfResurrect");
        public override bool ShouldRemove => false;

    }
}
