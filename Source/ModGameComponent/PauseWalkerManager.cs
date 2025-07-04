using PauseWalker.Defs;
using PauseWalker.Letter;
using PauseWalker.Utilities;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace PauseWalker.ModGameComponent
{
    public class PauseWalkerManager : GameComponent
    {
        private Dictionary<int, Pawn> tracked = new Dictionary<int, Pawn>();
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

            // 定期检查一下有没有不在 tracked 中的 pawn
            if (tickCounter % (checkInterval * 10) == 0)
            {

                List<Pawn> pauseWalkers = PawnsFinder.All_AliveOrDead.FindAll(p =>
                {
                    return Utils.HasPauseWalkerAbility(p) && p.IsColonist;
                });
                foreach (var item in pauseWalkers)
                {
                    if (!tracked.ContainsKey(item.thingIDNumber))
                    {
                        Log.Message("[PauseWalker] found unregisted PauseWalker" + item.Name);
                        this.Register(item);
                    }
                }

                tickCounter = 0;
            }


            HashSet<int> toBeRemoved = new HashSet<int>();

            // 检测是否有需要复活的 pawn
            foreach (var pair in tracked.ToList())
            {
                var id = pair.Key;
                var trackedPawn = pair.Value;

                Log.Message("[PauseWalker] Current PauseWalker id: " + id + " Name: " + trackedPawn.Name);
                Log.Message("[PauseWalker] Current PauseWalker id: " + id + " Dead: " + trackedPawn.Dead + " Destroyed: " + trackedPawn.Destroyed + " Discarded: " + trackedPawn.Discarded);

                if (trackedPawn == null)
                {
                    toBeRemoved.Add(id);
                    continue;
                }
                if (!Utils.HasPauseWalkerAbility(trackedPawn))
                {
                    toBeRemoved.Add(id);
                    continue;
                }
                if (!trackedPawn.IsColonist)
                {
                    toBeRemoved.Add(id);
                    continue;
                }
                if (!trackedPawn.Dead && trackedPawn.MapHeld != null) continue;
                //if(!trackedPawn.Dead && trackedPawn.IsFreeColonist) continue;

                if (trackedPawn.Dead && !Utils.HasUsableCorpse(trackedPawn))
                {
                    TryReviveThroughLetter(trackedPawn);
                }

                if (trackedPawn.Destroyed && !Utils.HasUsableCorpse(trackedPawn))
                {
                    TryReviveDestroyedThroughLetter(trackedPawn);
                }

                //if (trackedPawn.Discarded && !Utils.HasUsableCorpse(trackedPawn))
                //{
                //    Messages.Message(trackedPawn.Name + " is discarded but revived", trackedPawn, MessageTypeDefOf.PositiveEvent);
                //    trackedPawn.ForceSetStateToUnspawned();
                //    TryReviveThroughLetter(trackedPawn);
                //}

                if (!trackedPawn.Dead)
                {
                    if (KidnapUtility.IsKidnapped(trackedPawn))
                    {
                        KidnapPawnReturn(trackedPawn);
                    }
                }

            }

            foreach (var id in toBeRemoved)
            {
                tracked.Remove(id);
            }


        }


        private void TryReviveThroughLetter(Pawn pawn)
        {
            if (!LetterStackExists(pawn))
            {
                Log.Message($"[PauseWalker] Try revive pawn through letter: {pawn}");
                ChoiceLetter_PauseWalkerReturn letter = MakePauseWalkerLetter(pawn);
                Find.LetterStack.ReceiveLetter(letter, null, 0, true);
            }
        }

        private void TryReviveDestroyedThroughLetter(Pawn pawn)
        {
            if (!LetterStackExists(pawn))
            {
                Log.Message($"[PauseWalker] Try revive destroyed pawn through letter: {pawn}");
                //pawn.ForceSetStateToUnspawned();
                ChoiceLetter_PauseWalkerReturn letter = MakePauseWalkerLetter(pawn);
                Find.LetterStack.ReceiveLetter(letter, null, 0, true);
            }
        }

        private bool LetterStackExists(Pawn pawn)
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

        private ChoiceLetter_PauseWalkerReturn MakePauseWalkerLetter(Pawn pawn)
        {
            ChoiceLetter_PauseWalkerReturn letter = (ChoiceLetter_PauseWalkerReturn)LetterMaker.MakeLetter(PauseWalkerReturnLetterDefOf.PauseWalkerReturnLetter);
            letter.Pawn = pawn;
            letter.Label = "PauseWalker.PauseWalkerReturnLetterName".Translate(pawn);
            letter.Text = "PauseWalker.PauseWalkerReturnLetterText".Translate(pawn);
            letter.PawnThingIDNumber = pawn.thingIDNumber;
            return letter;
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
            if (pawn == null || tracked.ContainsKey(pawn.thingIDNumber)) return;

            if (!Utils.HasPauseWalkerAbility(pawn)) return;

            tracked[pawn.thingIDNumber] = pawn;

            Log.Message($"[PauseWalkerManager] added pawn：{pawn.LabelCap}");
        }


        public void RegisterThroughAbility(Pawn pawn)
        {
            if (pawn == null || tracked.ContainsKey(pawn.thingIDNumber)) return;
            tracked[pawn.thingIDNumber] = pawn;
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
