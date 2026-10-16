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
            if (!Utils.IsPauseWalkerPawn(pawn) || Find.TickManager?.CurTimeSpeed != TimeSpeed.Paused)
            {
                return;
            }
            base.Apply(target, dest);

            Map map = parent.pawn.Map;
            IntVec3 position = target.Cell;

            ThingDef roadrollerDef = DefDatabase<ThingDef>.GetNamed("RoadRollerIncoming");
 
            SkyfallerMaker.SpawnSkyfaller(roadrollerDef, position, map);
        }

    }
}
