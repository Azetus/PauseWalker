using PauseWalker.Defs;
using PauseWalker.Utilities;
using RimWorld;
using Verse;

namespace PauseWalker.Letter
{
    public class ChoiceLetter_PauseWalkerReturn : ChoiceLetter
    {
        private Pawn pawn;
        private int pawnThingIDNumber = -1;

        public ChoiceLetter_PauseWalkerReturn() : base()
        {

        }

        public Pawn Pawn
        {
            get
            {
                return this.pawn;
            }
            set
            {
                this.pawn = value;
            }
        }

        public int PawnThingIDNumber
        {
            get
            {
                return this.pawnThingIDNumber;
            }
            set
            {
                this.pawnThingIDNumber = value;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref pawnThingIDNumber, "pawnThingIDNumber");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                pawn = FindPawnByThingIDNumber(pawnThingIDNumber);
            }
        }


        public override bool CanDismissWithRightClick
        {
            get
            {
                return false;
            }
        }

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                if (base.ArchivedOnly || pawn == null || this.pawn.Spawned)
                {
                    yield return base.Option_Close;
                }
                else
                {
                    yield return Accept;
                    yield return Postpone;
                }

                yield break;
            }
        }

        protected DiaOption Postpone
        {
            get
            {
                DiaOption diaOption = new DiaOption("PostponeLetter".Translate());
                diaOption.resolveTree = true;
                return diaOption;
            }
        }

        protected DiaOption Accept
        {
            get
            {
                return new DiaOption("Accept".Translate())
                {
                    action = () =>
                    {
                        if (Current.Game.CurrentMap != null)
                        {
                            BeginCellSelectionInMap(Current.Game.CurrentMap);
                        }
                    },
                    resolveTree = true
                };
            }
        }
        private void BeginCellSelectionInMap(Map map)
        {
            Find.Targeter.BeginTargeting(
                TargetingParameters.ForCell(),
                (LocalTargetInfo target) =>
                {
                    if (target.IsValid && IsValidSpawnCell(target.Cell, map))
                    {
                        PauseWalkerReturnEffecterDefOf.PauseWalker_Return.Spawn().Trigger(target.ToTargetInfo(map), null);
                        Utils.TryRevive(this.pawn, target.Cell, map);
                        Find.LetterStack.RemoveLetter(this);
                    }
                }
            );
        }

        private bool IsValidSpawnCell(IntVec3 cell, Map map)
        {
            return cell.InBounds(map) &&
                   cell.Standable(map) &&
                   !cell.Fogged(map);
        }


        private Pawn? FindPawnByThingIDNumber(int thingIDNumber)
        {
            if (thingIDNumber < 0) return null;
            
            return Find.WorldPawns.AllPawnsAliveOrDead.FirstOrDefault(p => p.thingIDNumber == thingIDNumber);
        }


        public static bool LetterStackExists(Pawn pawn)
        {
            return Find.LetterStack.LettersListForReading.Exists(L =>
            {
                if (L is ChoiceLetter_PauseWalkerReturn pauseWalkerLetter && pauseWalkerLetter.Pawn == pawn)
                {
                    return true;
                }
                return false;
            });
        }

        public static ChoiceLetter_PauseWalkerReturn MakePauseWalkerLetter(Pawn pawn)
        {
            ChoiceLetter_PauseWalkerReturn letter = (ChoiceLetter_PauseWalkerReturn)LetterMaker.MakeLetter(PauseWalkerReturnLetterDefOf.PauseWalkerReturnLetter);
            letter.Pawn = pawn;
            letter.Label = "PauseWalker.PauseWalkerReturnLetterName".Translate(pawn);
            letter.Text = "PauseWalker.PauseWalkerReturnLetterText".Translate(pawn);
            letter.PawnThingIDNumber = pawn.thingIDNumber;
            return letter;
        }

    }

}
