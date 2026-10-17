using LudeonTK;
using PauseWalker.ModGameComponent;
using PauseWalker.Utilities;
using RimWorld;
using Verse;

namespace PauseWalker.Debug
{
    public static class PauseWalkerDebugActions
    {
        [DebugAction("PauseWalker", "PauseWalker Manager", allowedGameStates = AllowedGameStates.Playing)]
        public static void OpenManager()
        {
            var manager = Current.Game.GetComponent<PauseWalkerManager>();
            var options = manager.KnownPawns().OrderBy(pawn => pawn.LabelShort).Select(pawn =>
                Option(Summary(pawn, manager), () => OpenPawn(pawn, manager))).ToList();
            options.Add(Option("PauseWalker.Debug.Search".Translate(), () => FindUnmanaged(manager)));
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(options, "PauseWalker.Debug.Manager".Translate()));
        }

        private static DebugMenuOption Option(string label, Action action) => new(label, DebugMenuOptionMode.Action, action);

        private static string Summary(Pawn pawn, PauseWalkerManager manager) =>
            $"{pawn.LabelShortCap} [{pawn.thingIDNumber}] — {PauseWalkerStateUtility.Evaluate(pawn, manager).Description()}";

        private static void FindUnmanaged(PauseWalkerManager manager)
        {
            // 玩家点击时才取一次全局快照，绝不在 Tick/getter/世界视角中自动执行。
            var pawns = PawnsFinder.All_AliveOrDead.ToList();
            pawns.AddRange(manager.KnownPawns());
            var options = pawns.Where(pawn => pawn != null && !manager.IsTracked(pawn) && Utils.HasPauseWalkerAbility(pawn))
                .GroupBy(pawn => pawn.thingIDNumber).Select(group => group.First())
                .OrderBy(pawn => pawn.LabelShort)
                .Select(pawn => Option(Summary(pawn, manager), () => OpenPawn(pawn, manager))).ToList();
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(options, "PauseWalker.Debug.Search".Translate()));
        }

        private static void OpenPawn(Pawn pawn, PauseWalkerManager manager)
        {
            string header = Summary(pawn, manager) + $"\nFaction={pawn.Faction?.Name ?? "-"}; " +
                $"Map={pawn.MapHeld?.uniqueID.ToString() ?? "-"}; Holder={pawn.ParentHolder?.GetType().Name ?? "-"}; " +
                $"Dead={pawn.Dead}; Destroyed={pawn.Destroyed}; Discarded={pawn.Discarded}; " +
                $"Tracked={manager.IsTracked(pawn)}; Exclusion={manager.GetExclusion(pawn)}";
            var options = new List<DebugMenuOption>
            {
                Option("PauseWalker.Debug.Disable".Translate(), () => manager.Exclude(pawn, ManagementExclusion.Manual)),
                Option("PauseWalker.Debug.Restore".Translate(), () => manager.RestoreManagement(pawn)),
                Option((pawn.Faction == Faction.OfPlayer ? "PauseWalker.Debug.Return" : "PauseWalker.Debug.Reclaim").Translate(),
                    () => BeginReturn(pawn))
            };
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(options, header));
        }

        private static void BeginReturn(Pawn pawn)
        {
            Map? map = Find.CurrentMap;
            if (map == null)
            {
                Messages.Message("PauseWalker.Return.InvalidCell".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            Find.Targeter.BeginTargeting(TargetingParameters.ForCell(), target =>
            {
                if (target.IsValid)
                    PauseWalkerReturnUtility.ShowResult(pawn, PauseWalkerReturnUtility.TryReturn(pawn, target.Cell, map, force: true));
            });
        }
    }
}

