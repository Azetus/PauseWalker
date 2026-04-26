using PauseWalker.Defs;
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
                    defaultLabel = "PauseWalker.ReviveLabel".Translate(),
                    defaultDesc = "PauseWalker.ReviveDesc".Translate(),
                    icon = PauseWalkerResurrectHediff.Icon.Texture,
                    action = () =>
                    {
                        this.Use();
                    }
                };
            }
            yield break;
        }

        private void Use()
        {
            Messages.Message("PauseWalker.ReviveMessage".Translate(this.pawn), this.pawn, MessageTypeDefOf.PositiveEvent, true);

            PauseWalkerReturnEffecterDefOf.PauseWalker_Return.Spawn().Trigger(pawn, null);

            ResurrectionUtility.TryResurrect(this.pawn);
        }

        public bool PlayerControlled
        {
            get
            {
                return this.pawn.IsColonist && (this.pawn.HostFaction == null || this.pawn.IsSlave);
            }
        }
        private static readonly CachedTexture Icon = new CachedTexture("Icon/PauseWalkerReviveIcon");
        public override bool ShouldRemove => false;

    }
}
