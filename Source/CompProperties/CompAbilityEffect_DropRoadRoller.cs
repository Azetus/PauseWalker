using PauseWalker.Utilities;
using RimWorld;
using Verse;

namespace PauseWalker.CompProperties
{
    public class CompAbilityEffect_DropRoadRoller : CompAbilityEffect
    {
        public new CompProperties_AbilityEffect_DropRoadRoller Props
        {
            get
            {
                return (CompProperties_AbilityEffect_DropRoadRoller)this.props;
            }
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            Pawn pawn = parent.pawn;
            if (!Utils.IsPauseWalkerPawn(pawn))
            {
                return;
            }
            base.Apply(target, dest);

            Map map = parent.pawn.Map;
            IntVec3 position = target.Cell;

            ThingDef roadrollerDef = DefDatabase<ThingDef>.GetNamed("RoadRollerIncoming");
 
            SkyfallerMaker.SpawnSkyfaller(roadrollerDef, position, map);
        }

        public override bool GizmoDisabled(out string reason)
        {
            Pawn pawn = parent.pawn;
            if (!Utils.IsPauseWalkerPawn(pawn)) {
                List<string> reasons = new List<string>();
                if (!Utils.HasPauseWalkerAbility(pawn)){
                    var notPauseWalker = "PauseWalker.NotPauseWalker".Translate();
                    reasons.Add(notPauseWalker);
                }

                if (!Utils.HasPauseWalkerHediff(pawn)){
                    var noPauseWalkerHediff = "PauseWalker.NoPauseWalkerHediff".Translate();
                    reasons.Add(noPauseWalkerHediff);
                }
                reason = string.Join("\n", reasons);
                return true;
            }

            reason = String.Empty;
            return false;
        }
    }
}
