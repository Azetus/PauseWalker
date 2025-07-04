using HarmonyLib;
using PauseWalker.Defs;
using RimWorld;
using Verse;
namespace PauseWalker.Utilities
{
    internal static class Utils
    {
        public static int GetRawTicksGameInt()
        {
            if (Current.Game != null && Find.TickManager != null)
            {
                if (AccessTools.Field(Find.TickManager.GetType(), "ticksGameInt") is { } rawTicksGameField &&
                    rawTicksGameField.GetValue(Find.TickManager) is int ticks)
                {
                    return ticks;
                }
            }

            return 0;
        }

        public static bool HasPauseWalkerAbility(Pawn? pawn)
        {
            if (pawn == null || pawn.abilities == null) return false;
            return pawn.abilities.GetAbility(PauseWalkerAbilityDefOf.PauseWalkerAbility) != null;
        }

        public static bool HasPauseWalkerHediff(Pawn? pawn)
        {
            if (pawn == null || pawn.health == null || pawn.health.hediffSet == null) return false;
            return pawn.health.hediffSet.HasHediff(PauseWalkerHediffDefOf.PauseWalkerHediff);
        }

        // 用小人身上的状态效果(Hediff)判断该小人能否在暂停时移动
        public static bool IsPauseWalkerPawn(Pawn? pawn)
        {
            if (pawn == null) return false;
            bool hasPauseWalkerAbility = HasPauseWalkerAbility(pawn);
            bool hasPauseWalkerHediff = HasPauseWalkerHediff(pawn);
            return hasPauseWalkerAbility && hasPauseWalkerHediff;
        }

        public static bool CurrentMapContainsPauseWalker(Map currentMap)
        {
            if (Current.Game != null && Find.TickManager != null && currentMap != null && currentMap.mapPawns != null)
            {
                var spawnedPawns = currentMap.mapPawns.AllPawnsSpawned;
                if (spawnedPawns.Count > 0)
                {
                    return spawnedPawns.Any(pawn =>
                    {
                        return IsPauseWalkerPawn(pawn);
                    });
                }
            }

            return false;
        }

        public static void RemoveHediffAndAbilityFromPawn(Pawn pawn)
        {
            // 移除小人身上的PauseWalker相关状态与技能
            if (pawn == null) return;

            if (pawn.health?.hediffSet != null)
            {
                var existing = pawn.health.hediffSet.GetFirstHediffOfDef(PauseWalkerHediffDefOf.PauseWalkerHediff);
                if (existing != null)
                {
                    pawn.health.RemoveHediff(existing);
                }
            }
            if (HasPauseWalkerAbility(pawn))
            {
                pawn.abilities.RemoveAbility(DropRoadRollerAbilityDefOf.DropRoadRollerAbility);
                pawn.abilities.RemoveAbility(PauseWalker_ThrowKnifeAbilityDefOf.PauseWalker_ThrowKnife);
            }

        }


        public static bool HasUsableCorpse(Pawn pawn)
        {
            Corpse corpse = pawn.Corpse;
            return corpse != null && !corpse.Destroyed && corpse.Spawned && corpse.MapHeld != null;
        }

        public static void TryRevive(Pawn trackedPawn, IntVec3 loc, Map map)
        {
            if (trackedPawn == null)
            {
                Log.Error($"[PauseWalker] Try revive pawn from world failed: trackedPawn is null.");
                return;
            }

            Pawn? found = Find.WorldPawns.AllPawnsDead.FirstOrDefault(p => p.ThingID == trackedPawn.ThingID);
            if (found != null)
            {
                if (found.Dead)
                {
                    bool success = ResurrectionUtility.TryResurrect(found);
                    if (!success) {
                        Log.Warning($"[PauseWalker] Resurrection failed for {found}.");
                    }
                }
                if (found.Destroyed || found.Discarded)
                {
                    found.ForceSetStateToUnspawned();
                }
                GenSpawn.Spawn(found, loc, map);
                Log.Message($"[PauseWalker] Try revive from world: {found}");
                return;
            }

            Log.Warning($"[PauseWalker] Try revive {trackedPawn.LabelCap} failed: can not found pawn in WorldPawns.");
            return;
        }
    }
}
