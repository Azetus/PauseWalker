using PauseWalker.Defs;
using PauseWalker.ModGameComponent;
using RimWorld;
using RimWorld.Planet;
using Verse;
namespace PauseWalker.Utilities
{
    internal static class Utils
    {
        public static int GetRawTicksGameInt()
        {
            if (Current.Game != null && Find.TickManager != null)
            {
                return Find.TickManager.ticksGameInt;
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
            if (currentMap == null) return false;
            var manager = PauseWalkerManager.Instance;
            if (manager != null)
                return manager.HasActivePauseWalkerOnMap(currentMap);

            var pawns = currentMap.mapPawns.AllPawnsSpawned;
            return pawns.Count > 0 && pawns.Any(pawn => IsPauseWalkerPawn(pawn));
        }

        public static void RemovePauseWalkerHediff(Pawn pawn)
        {
            // 子技能由 Hediff 提供，随 Hediff 一起退出有效能力列表。
            if (pawn == null) return;

            if (pawn.health?.hediffSet != null)
            {
                var existing = pawn.health.hediffSet.GetFirstHediffOfDef(PauseWalkerHediffDefOf.PauseWalkerHediff);
                if (existing != null)
                {
                    pawn.health.RemoveHediff(existing);
                }
            }
        }


        // 墓穴、容器、搬运及运输中的尸体仍存在，自动回归不能将其当作丢失。
        public static bool HasUsableCorpse(Pawn pawn) => pawn.Corpse != null && !pawn.Corpse.Destroyed;
    }
}
