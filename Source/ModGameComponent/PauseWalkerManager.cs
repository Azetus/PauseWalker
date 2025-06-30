using Verse;
using RimWorld;
using PauseWalker.Utilities;
using RimWorld.Planet;

namespace PauseWalker.ModGameComponent
{
    public class PauseWalkerManager : GameComponent
    {
        private Dictionary<string, Pawn> tracked = new Dictionary<string, Pawn>();
        private int checkInterval = 60;
        private int tickCounter = 0;

        public PauseWalkerManager(Game game) : base()
        {
            Log.Message("[PauseWalker] PauseWalkerManager");
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();

            tickCounter++;
            if (tickCounter % checkInterval != 0) return;

            if (tickCounter % (checkInterval * 10) == 0)
            {

                List<Pawn> pauseWalkers = PawnsFinder.All_AliveOrDead.FindAll(p => Utils.HasPauseWalkerAbility(p));
                foreach (var item in pauseWalkers)
                {
                    if (!tracked.ContainsKey(item.ThingID))
                    {
                        Log.Message("[PauseWalker] 发现未注册的PauseWalker" + item.Name);
                        this.Register(item);
                    }
                }

                tickCounter = 0;
            }




            foreach (var pair in tracked.ToList())
            {
                var id = pair.Key;
                var trackedPawn = pair.Value;

                Log.Message("[PauseWalker] Current PauseWalker id: " + id + " Name: " + trackedPawn.Name);
                Log.Message("[PauseWalker] Current PauseWalker id: " + id + " Dead: " + trackedPawn.Dead + " Destroyed: " + trackedPawn.Destroyed + " Discarded: " + trackedPawn.Discarded);

                if (trackedPawn == null) continue;
                if (!trackedPawn.IsColonist) continue;
                if(!trackedPawn.Dead &&trackedPawn.MapHeld != null) continue;
                //if(!trackedPawn.Dead && trackedPawn.IsFreeColonist) continue;

                if (trackedPawn.Dead && !Utils.HasUsableCorpse(trackedPawn))
                {
                    Messages.Message(trackedPawn.Name + " is revived", trackedPawn, MessageTypeDefOf.PositiveEvent);
                    TryRevive(trackedPawn);
                }

                if (trackedPawn.Destroyed && !Utils.HasUsableCorpse(trackedPawn))
                {
                    //trackedPawn.Kill(null);
                    Messages.Message(trackedPawn.Name + " is destroyed but revived", trackedPawn, MessageTypeDefOf.PositiveEvent);
                    //TryRevive(trackedPawn);
                    trackedPawn.ForceSetStateToUnspawned();
                    GenSpawn.Spawn(trackedPawn, CellFinder.RandomClosewalkCellNear(MapGenerator.PlayerStartSpot, Find.CurrentMap, 5), Find.CurrentMap);
                }

                if (trackedPawn.Discarded && !Utils.HasUsableCorpse(trackedPawn))
                {
                    //trackedPawn.Kill(null);
                    Messages.Message(trackedPawn.Name + " is discarded but revived", trackedPawn, MessageTypeDefOf.PositiveEvent);
                    //TryRevive(trackedPawn);
                    trackedPawn.ForceSetStateToUnspawned();
                    GenSpawn.Spawn(trackedPawn, CellFinder.RandomClosewalkCellNear(MapGenerator.PlayerStartSpot, Find.CurrentMap, 5), Find.CurrentMap);
                }

                if (!trackedPawn.Dead)
                {
                    if (KidnapUtility.IsKidnapped(trackedPawn))
                    {
                        KidnapPawnReturn(trackedPawn);
                    }
                }

            }



        }



        private void TryRevive(Pawn trackedPawn)
        {
            if (trackedPawn == null)
                return;

            if (trackedPawn.Corpse != null)
            {
                // 尸体还在地图
                ResurrectionUtility.TryResurrect(trackedPawn);
                Log.Message($"[RevivalManager] 通过尸体复活：{trackedPawn}");
                return;
            }

            // WorldPawns 中找尸体已销毁但 Pawn 尚在的情况
            var found = Find.WorldPawns.AllPawnsDead.FirstOrDefault(p => p.ThingID == trackedPawn.ThingID);
            if (found != null)
            {
                ResurrectionUtility.TryResurrect(found);
                GenSpawn.Spawn(found, CellFinder.RandomClosewalkCellNear(MapGenerator.PlayerStartSpot, Find.CurrentMap, 5), Find.CurrentMap);
                Log.Message($"[RevivalManager] 从世界中复活：{found}");
                return;
            }

            return;
        }

        public void Register(Pawn pawn)
        {
            if (pawn == null || tracked.ContainsKey(pawn.ThingID)) return;

            if (!Utils.HasPauseWalkerAbility(pawn)) return;

            tracked[pawn.ThingID] = pawn;

            Log.Message($"[PauseWalkerManager] added pawn：{pawn.LabelCap}");
        }


        public void RegisterThroughAbility(Pawn pawn)
        {
            if (pawn == null || tracked.ContainsKey(pawn.ThingID)) return;
            tracked[pawn.ThingID] = pawn;
            Log.Message($"[PauseWalkerManager] added pawn：{pawn.LabelCap}");

        }


        public bool IsAbandonedColonist(Pawn pawn)
        {
            if (pawn == null)
                return false;

            if (!pawn.IsColonist)
                return false;

            bool isAbandoned = pawn.Faction == Faction.OfPlayer // 玩家派系
                               && !pawn.Dead
                               && !pawn.Discarded
                               && !pawn.Spawned
                               && pawn.MapHeld == null
                               && pawn.GetCaravan() == null;

            return isAbandoned;
        }

        public void KidnapPawnReturn(Pawn pawn)
        {
            List<Faction> allFactionsListForReading = Find.FactionManager.AllFactionsListForReading;
            foreach (var item in allFactionsListForReading)
            {
                if (item.kidnapped.KidnappedPawnsListForReading.Contains(pawn))
                {
                    item.kidnapped.RemoveKidnappedPawn(pawn);
                    RecallPawnToMap(pawn);
                }
            }
        }

        public void RecallPawnToMap(Pawn pawn)
        {
            Map map = Find.CurrentMap;
            if (pawn.Spawned || pawn.Dead)
                return;

            if (map == null)
            {
                Log.Warning("Recall failed: target map is null.");
                return;
            }

            // 设置为玩家派系
            if (pawn.Faction != Faction.OfPlayer)
            {
                pawn.SetFaction(Faction.OfPlayer);
            }

            // 若不在WorldPawns中，则加入WorldPawns
            if (!Find.WorldPawns.Contains(pawn))
            {
                Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.KeepForever);
            }
            Log.Message("RecallPawnToMap pawn: " + pawn.LabelCap);

            GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(MapGenerator.PlayerStartSpot, Find.CurrentMap, 5), map, Rot4.Random, WipeMode.Vanish);
        }


    }
}
