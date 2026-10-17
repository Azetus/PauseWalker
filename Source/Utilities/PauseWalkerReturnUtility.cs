using PauseWalker.ModGameComponent;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace PauseWalker.Utilities
{
    public readonly struct ReturnResult
    {
        public bool Success { get; }
        public string Reason { get; }
        private ReturnResult(bool success, string reason) { Success = success; Reason = reason; }
        public static ReturnResult Done() => new(true, string.Empty);
        public static ReturnResult Fail(string key) => new(false, key.Translate());
        public static ReturnResult Blocked(PauseWalkerState state) => new(false, state.Description());
    }

    public static class PauseWalkerReturnUtility
    {
        public static bool IsValidCell(IntVec3 cell, Map? map) => Current.Game != null && map != null
            && Find.Maps != null && Find.Maps.Contains(map)
            && cell.InBounds(map) && cell.Standable(map) && !cell.Fogged(map);

        public static ReturnResult TryReturn(Pawn? pawn, IntVec3 cell, Map map, bool force = false)
        {
            if (!IsValidCell(cell, map)) return ReturnResult.Fail("PauseWalker.Return.InvalidCell");
            if (pawn == null || pawn.Discarded || !PauseWalkerStateUtility.HasRequiredData(pawn))
                return ReturnResult.Fail("PauseWalker.Return.InvalidPawn");

            var manager = Current.Game?.GetComponent<PauseWalkerManager>();
            if (manager == null) return ReturnResult.Blocked(PauseWalkerState.Unmanaged);
            PauseWalkerState state = PauseWalkerStateUtility.Evaluate(pawn, manager);
            if (!force && !state.CanReturn()) return ReturnResult.Blocked(state);
            // 强制模式也不复制地图上的活人，不抢原版正在执行的转场或任务。
            if (force && (pawn.Spawned || (!pawn.Dead && pawn.MapHeld != null)))
                return ReturnResult.Blocked(PauseWalkerState.OnMap);
            // 手动排除、外派阵营会遮蔽集中判定的后续分支，因此强制模式独立检查正在执行的流程。
            if (force && (Find.CurrentGravship?.Pawns.Contains(pawn) == true ||
                (ModsConfig.AnomalyActive && Find.Anomaly?.metalHellPawns.Contains(pawn) == true) ||
                pawn.IsBorrowedByAnyFaction() || QuestUtility.IsReservedByQuestOrQuestBeingGenerated(pawn) || pawn.teleporting ||
                pawn.health.hediffSet.hediffs.OfType<Hediff_DeathRefusal>().Any(hediff => hediff.InProgress)))
                return ReturnResult.Fail("PauseWalker.Return.Protected");
            if (force && !Utils.HasPauseWalkerAbility(pawn)) return ReturnResult.Blocked(PauseWalkerState.NoAbility);
            // 未知持有者可能还有 mod 自己的索引，不只移除 ThingOwner 就假定清理完整。
            if (force && !pawn.Dead && pawn.ParentHolder != null &&
                pawn.ParentHolder is not ActiveTransporterInfo && pawn.ParentHolder is not CompTransporter &&
                pawn.ParentHolder is not Caravan)
                return ReturnResult.Blocked(PauseWalkerState.Held);

            try
            {
                if (pawn.Dead)
                {
                    // 远程复活由本方法选择落点，避免原版按旧尸体位置先生成一次。
                    if (!ResurrectionUtility.TryResurrect(pawn, new ResurrectionParams { dontSpawn = true }))
                        return ReturnResult.Fail("PauseWalker.Return.ResurrectionFailed");
                    if (pawn.Dead || pawn.Discarded) return ReturnResult.Fail("PauseWalker.Return.ResurrectionFailed");
                }
                else if (pawn.Destroyed)
                    pawn.ForceSetStateToUnspawned();

                if (force)
                {
                    pawn.GetCaravan()?.RemovePawn(pawn);
                    if (pawn.holdingOwner != null && !pawn.holdingOwner.Remove(pawn))
                        return ReturnResult.Fail("PauseWalker.Return.DetachFailed");
                }
                // SpawnSetup 会自行将成功生成的 Pawn 从 WorldPawns 注销。
                GenSpawn.Spawn(pawn, cell, map);
                if (!pawn.Spawned || pawn.Map != map || pawn.Dead || pawn.Destroyed)
                    return ReturnResult.Fail("PauseWalker.Return.SpawnFailed");

                if (force && pawn.Faction != Faction.OfPlayer) pawn.SetFaction(Faction.OfPlayer);

                // 只有实际回归成功才解除绑架记录，推迟信件不会改变原版状态。
                foreach (Faction faction in Find.FactionManager.AllFactionsListForReading)
                    if (faction.kidnapped.KidnappedPawnsListForReading.Contains(pawn))
                        faction.kidnapped.RemoveKidnappedPawn(pawn);
                if (force) manager?.RestoreManagement(pawn);
                ChoiceLetterCancel(pawn);
                return ReturnResult.Done();
            }
            catch (Exception ex)
            {
                // 复活或生成可能已经发生部分变化，不继续执行，也不伪造事务回滚。
                Log.Error($"[PauseWalker] Return failed for pawn id={pawn.thingIDNumber}: {ex}");
                return ReturnResult.Fail("PauseWalker.Return.Exception");
            }
        }

        public static ReturnResult TryResurrectCorpse(Pawn pawn)
        {
            var manager = Current.Game?.GetComponent<PauseWalkerManager>();
            if (manager?.IsTracked(pawn) != true) return ReturnResult.Blocked(PauseWalkerState.Unmanaged);
            PauseWalkerState state = PauseWalkerStateUtility.Evaluate(pawn, manager);
            if (state != PauseWalkerState.CorpsePresent) return ReturnResult.Blocked(state);
            if (!pawn.Dead || pawn.Corpse?.MapHeld == null || !pawn.Corpse.SpawnedOrAnyParentSpawned)
                return ReturnResult.Fail("PauseWalker.Return.InvalidPawn");
            try
            {
                if (!ResurrectionUtility.TryResurrect(pawn) || pawn.Dead || !pawn.Spawned)
                    return ReturnResult.Fail("PauseWalker.Return.ResurrectionFailed");
                ChoiceLetterCancel(pawn);
                return ReturnResult.Done();
            }
            catch (Exception ex)
            {
                Log.Error($"[PauseWalker] Corpse resurrection failed for pawn id={pawn.thingIDNumber}: {ex}");
                return ReturnResult.Fail("PauseWalker.Return.Exception");
            }
        }

        private static void ChoiceLetterCancel(Pawn pawn) => Letter.ChoiceLetter_PauseWalkerReturn.CancelFor(pawn);

        public static void ShowResult(Pawn pawn, ReturnResult result)
        {
            if (!result.Success)
            {
                Messages.Message("PauseWalker.Return.Failed".Translate(result.Reason), MessageTypeDefOf.RejectInput, false);
                return;
            }
            Messages.Message("PauseWalker.Return.Success".Translate(pawn.LabelShortCap), pawn, MessageTypeDefOf.PositiveEvent, true);
            // 表现层失败不把已经完成的回归重新报告为失败。
            try { Defs.PauseWalkerReturnEffecterDefOf.PauseWalker_Return.Spawn().Trigger(pawn, null); }
            catch (Exception ex) { Log.Warning($"[PauseWalker] Return effect failed: {ex.Message}"); }
        }
    }
}

