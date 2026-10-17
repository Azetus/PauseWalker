using PauseWalker.Defs;
using PauseWalker.Utilities;
using PauseWalker.ModGameComponent;
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

            if (this.pawn.Dead && this.PlayerControlled &&
                Current.Game?.GetComponent<PauseWalkerManager>()?.IsTracked(pawn) == true && PauseWalkerStateUtility.Evaluate(pawn,
                Current.Game?.GetComponent<PauseWalkerManager>()) == PauseWalkerState.CorpsePresent)
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
            var result = PauseWalkerReturnUtility.TryResurrectCorpse(pawn);
            PauseWalkerReturnUtility.ShowResult(pawn, result);
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
