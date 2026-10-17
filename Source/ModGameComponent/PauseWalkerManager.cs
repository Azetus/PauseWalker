using PauseWalker.Defs;
using PauseWalker.Letter;
using PauseWalker.Utilities;
using RimWorld;
using Verse;

namespace PauseWalker.ModGameComponent
{
    public enum ManagementExclusion { None, Manual, Departed }

    public class PauseWalkerManager : GameComponent
    {
        public static PauseWalkerManager? Instance { get; private set; }
        private readonly Dictionary<int, Pawn> tracked = new();
        // 仅保留本次会话遇到的引用供调试使用；不深度保存或强制保留 WorldPawn。
        private readonly Dictionary<int, Pawn> known = new();
        private Dictionary<int, ManagementExclusion> exclusions = new();
        private const int CheckInterval = 7200;
        private int tickCounter;

        public PauseWalkerManager(Game game) { Instance = this; }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref exclusions, "pauseWalkerManagementExclusions", LookMode.Value, LookMode.Value);
            exclusions ??= new();
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            // 构造回调可能早于排除名单和能力列表载入，在完整载入后统一校验。
            foreach (Pawn pawn in tracked.Values.ToList())
                if (PauseWalkerStateUtility.Evaluate(pawn, this).ShouldUnregister()) Unregister(pawn);
        }

        public bool IsTracked(Pawn pawn) => tracked.TryGetValue(pawn.thingIDNumber, out Pawn? value) && value == pawn;
        public ManagementExclusion GetExclusion(Pawn pawn) => exclusions.TryGetValue(pawn.thingIDNumber, out var value)
            ? value : ManagementExclusion.None;
        public List<Pawn> KnownPawns() => known.Values.Where(p => p != null).ToList();

        public void RegisterThroughAbility(Pawn pawn)
        {
            if (pawn == null) return;
            known[pawn.thingIDNumber] = pawn;
            // 构造中的能力尚未加入 Pawn 列表，不能在这里用 GetAbility 再验证。
            if (GetExclusion(pawn) == ManagementExclusion.None &&
                (Scribe.mode != LoadSaveMode.Inactive || (pawn.IsColonist && !pawn.IsQuestLodger())))
                tracked[pawn.thingIDNumber] = pawn;
        }

        public void NotifyMembershipChanged(Pawn pawn, bool recruited)
        {
            if (!known.ContainsKey(pawn.thingIDNumber) && !Utils.HasPauseWalkerAbility(pawn)) return;
            known[pawn.thingIDNumber] = pawn;
            if (!pawn.IsColonist || !Utils.HasPauseWalkerAbility(pawn) || pawn.IsQuestLodger())
            {
                Unregister(pawn);
                return;
            }
            // 明确重新加入的存活角色可撤销正式离队，手动停用始终保留。
            if (recruited && !pawn.Dead && !pawn.Destroyed && GetExclusion(pawn) == ManagementExclusion.Departed)
                exclusions.Remove(pawn.thingIDNumber);
            if (GetExclusion(pawn) == ManagementExclusion.None) tracked[pawn.thingIDNumber] = pawn;
        }

        public void Exclude(Pawn pawn, ManagementExclusion reason)
        {
            known[pawn.thingIDNumber] = pawn;
            // 正式离队不能覆盖玩家已经做出的手动停用决定。
            if (GetExclusion(pawn) != ManagementExclusion.Manual) exclusions[pawn.thingIDNumber] = reason;
            Unregister(pawn);
        }

        public void RestoreManagement(Pawn pawn)
        {
            exclusions.Remove(pawn.thingIDNumber);
            NotifyMembershipChanged(pawn, recruited: false);
        }

        public void Unregister(Pawn pawn)
        {
            tracked.Remove(pawn.thingIDNumber);
            ChoiceLetter_PauseWalkerReturn.CancelFor(pawn);
            if (!pawn.Destroyed) Utils.RemovePauseWalkerHediff(pawn);
        }

        public bool HasActivePauseWalkerOnMap(Map map)
        {
            if (map == null) return false;
            foreach (Pawn pawn in tracked.Values)
                if (pawn != null && !pawn.Destroyed && pawn.MapHeld == map && Utils.HasPauseWalkerHediff(pawn)) return true;
            return false;
        }

        public void ClearAllPauseWalkerHediffs()
        {
            foreach (Pawn pawn in tracked.Values.ToList())
                if (pawn != null && !pawn.Destroyed) Utils.RemovePauseWalkerHediff(pawn);
        }

        public override void GameComponentTick()
        {
            if (++tickCounter < CheckInterval) return;
            tickCounter = 0;
            foreach (Pawn pawn in tracked.Values.ToList())
            {
                PauseWalkerState state = PauseWalkerStateUtility.Evaluate(pawn, this);
                if (state.ShouldUnregister()) { Unregister(pawn); continue; }
                if (!pawn.Dead && !pawn.Destroyed && !pawn.Discarded && pawn.health?.hediffSet != null &&
                    !pawn.health.hediffSet.HasHediff(PauseWalkerResurrectHediffDefOf.PauseWalkerResurrectHediff))
                    pawn.health.AddHediff(PauseWalkerResurrectHediffDefOf.PauseWalkerResurrectHediff);
                if (state.CanReturn())
                {
                    if (!ChoiceLetter_PauseWalkerReturn.LetterStackExists(pawn))
                        Find.LetterStack.ReceiveLetter(ChoiceLetter_PauseWalkerReturn.MakePauseWalkerLetter(pawn), null, 0, true);
                }
                else if (state is PauseWalkerState.OnMap or PauseWalkerState.CorpsePresent)
                    ChoiceLetter_PauseWalkerReturn.CancelFor(pawn);
            }
        }
    }
}
