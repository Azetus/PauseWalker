using HarmonyLib;
using PauseWalker.ModGameComponent;
using PauseWalker.Utilities;
using RimWorld;
using Verse;

namespace PauseWalker.Patches
{
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SetFaction))]
    public static class PauseWalkerFactionPatch
    {
        public static void Prefix(Pawn __instance, out (bool Captured, Faction? Previous) __state) =>
            __state = (true, __instance.Faction);
        public static void Postfix(Pawn __instance, (bool Captured, Faction? Previous) __state)
        {
            // 其他 Prefix 可能取消调用并跳过本 Prefix，不能把默认 null 当成原派系。
            if (!__state.Captured || Scribe.mode != LoadSaveMode.Inactive) return;
            bool joined = __state.Previous != Faction.OfPlayer && __instance.Faction == Faction.OfPlayer;
            Current.Game?.GetComponent<PauseWalkerManager>()?.NotifyMembershipChanged(__instance, joined);
        }
    }

    [HarmonyPatch(typeof(RecruitUtility), nameof(RecruitUtility.Recruit))]
    public static class PauseWalkerRecruitPatch
    {
        public static void Postfix(Pawn pawn, Faction faction, bool __runOriginal)
        {
            if (__runOriginal && faction == Faction.OfPlayer)
                Current.Game?.GetComponent<PauseWalkerManager>()?.NotifyMembershipChanged(pawn, recruited: true);
        }
    }

    internal static class PauseWalkerDeparture
    {
        public static void ConfirmDestroyed(List<Pawn>? candidates)
        {
            if (candidates == null) return;
            foreach (Pawn pawn in candidates)
                if (pawn.Destroyed) Confirm(pawn);
        }

        public static void Confirm(Pawn pawn)
        {
            if (!Utils.HasPauseWalkerAbility(pawn)) return;
            Current.Game?.GetComponent<PauseWalkerManager>()?.Exclude(pawn, ManagementExclusion.Departed);
        }
    }

    [HarmonyPatch(typeof(ShipCountdown), "CountdownEnded")]
    public static class PauseWalkerShipEndingPatch
    {
        public static void Prefix(out List<Pawn> __state)
        {
            __state = new();
            Building? root = ShipCountdown.shipRoot;
            if (root == null) return;
            // 只记录实际发射舰体的休眠舱乘员，不影响留守者或取消倒计时。
            foreach (Building casket in ShipUtility.ShipBuildingsAttachedTo(root))
                if (casket is Building_CryptosleepCasket sleeper)
                    foreach (Thing thing in sleeper.GetDirectlyHeldThings())
                        if (thing is Pawn pawn && !pawn.Destroyed) __state.Add(pawn);
        }

        // 即使发射流程中途抛错，也只排除已经实际销毁的乘员；不吞掉原异常。
        public static void Finalizer(List<Pawn> __state) => PauseWalkerDeparture.ConfirmDestroyed(__state);
    }

    [HarmonyPatch(typeof(QuestPart_EndGame), nameof(QuestPart_EndGame.Notify_QuestSignalReceived))]
    public static class PauseWalkerRoyalEndingPatch
    {
        public static void Prefix(QuestPart_EndGame __instance, Signal signal, out List<Pawn> __state)
        {
            __state = new();
            // 与原版的提前返回条件一致，无关信号和正在倒计时的情况不记为离队。
            if (signal.tag != __instance.inSignal || ShipCountdown.CountingDown) return;
            if (signal.args.TryGetArg("SENTCOLONISTS", out List<Pawn> pawns) && pawns != null)
                __state.AddRange(pawns.Where(pawn => pawn != null && !pawn.Destroyed));
        }

        public static void Finalizer(List<Pawn> __state) => PauseWalkerDeparture.ConfirmDestroyed(__state);
    }

    [HarmonyPatch(typeof(MoveColonyUtility), nameof(MoveColonyUtility.MoveColonyAndReset))]
    public static class PauseWalkerNewColonyPatch
    {
        public static void Prefix(IEnumerable<Thing> colonyThings, out List<Pawn> __state)
        {
            // 原版本次转移范围的快照；只在卖出殖民地时取一次，不加入周期扫描。
            var carried = new HashSet<Thing>(colonyThings);
            __state = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_OfPlayerFaction
                .Where(pawn => !carried.Contains(pawn) && Utils.HasPauseWalkerAbility(pawn)).ToList();
        }

        public static void Postfix(List<Pawn>? __state, bool __runOriginal = true)
        {
            // 仅在原方法完成后排除明确放弃的角色，选中带走的角色继续受管理。
            if (!__runOriginal || __state == null) return;
            foreach (Pawn pawn in __state) PauseWalkerDeparture.Confirm(pawn);
        }
    }
}

