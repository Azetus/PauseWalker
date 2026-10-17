using PauseWalker.Defs;
using PauseWalker.ModGameComponent;
using PauseWalker.Utilities;
using RimWorld;
using Verse;

namespace PauseWalker.Letter
{
    public class ChoiceLetter_PauseWalkerReturn : ChoiceLetter
    {
        private Pawn? pawn;
        public Pawn? Pawn { get => pawn; set => pawn = value; }
        public override bool CanDismissWithRightClick => false;

        public override void ExposeData()
        {
            base.ExposeData();
            // 保存引用而非深度保存 Pawn；找不到数据时不凭 ThingID 创建角色。
            Scribe_References.Look(ref pawn, "pawn", saveDestroyedThings: true);
        }

        private PauseWalkerState CurrentState => PauseWalkerStateUtility.Evaluate(pawn,
            Current.Game?.GetComponent<PauseWalkerManager>());

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                PauseWalkerState state = CurrentState;
                if (ArchivedOnly || state.ShouldUnregister() || state is PauseWalkerState.OnMap or PauseWalkerState.CorpsePresent)
                {
                    yield return Option_Close;
                    yield break;
                }
                DiaOption accept = new("Accept".Translate()) { resolveTree = true, action = BeginSelection };
                if (!state.CanReturn()) accept.Disable(state.Description());
                yield return accept;
                yield return new DiaOption("PostponeLetter".Translate()) { resolveTree = true };
            }
        }

        private void BeginSelection()
        {
            // 点击接受时再次校验，不能把发信时的快照当成长期有效的许可。
            PauseWalkerState state = CurrentState;
            if (!state.CanReturn())
            {
                if (pawn != null) PauseWalkerReturnUtility.ShowResult(pawn, ReturnResult.Blocked(state));
                return;
            }
            Map? map = Current.Game?.CurrentMap;
            if (map == null)
            {
                Messages.Message("PauseWalker.Return.InvalidCell".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            Find.Targeter.BeginTargeting(TargetingParameters.ForCell(), target =>
            {
                if (!target.IsValid || pawn == null) return;
                // 选点期间状态和地图都可能变化，由执行器进行最终校验。
                ReturnResult result = PauseWalkerReturnUtility.TryReturn(pawn, target.Cell, map);
                PauseWalkerReturnUtility.ShowResult(pawn, result);
            });
        }

        public static bool LetterStackExists(Pawn pawn) => Find.LetterStack.LettersListForReading
            .OfType<ChoiceLetter_PauseWalkerReturn>().Any(letter => letter.pawn == pawn);

        public static void CancelFor(Pawn pawn)
        {
            if (Current.Game == null || Find.LetterStack == null) return;
            foreach (var letter in Find.LetterStack.LettersListForReading.OfType<ChoiceLetter_PauseWalkerReturn>()
                .Where(letter => letter.pawn == pawn).ToList())
                Find.LetterStack.RemoveLetter(letter);
        }

        public static ChoiceLetter_PauseWalkerReturn MakePauseWalkerLetter(Pawn pawn)
        {
            var letter = (ChoiceLetter_PauseWalkerReturn)LetterMaker.MakeLetter(PauseWalkerReturnLetterDefOf.PauseWalkerReturnLetter);
            letter.pawn = pawn;
            letter.Label = "PauseWalker.PauseWalkerReturnLetterName".Translate(pawn.LabelShortCap);
            letter.Text = "PauseWalker.PauseWalkerReturnLetterText".Translate(pawn.LabelShortCap);
            return letter;
        }
    }
}
