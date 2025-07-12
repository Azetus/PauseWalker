using PauseWalker.Defs;
using PauseWalker.Letter;
using PauseWalker.Utilities;
using RimWorld;
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
                // 需要找所有Pawn, 包括在Map中和World中的
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

                //Log.Message("[PauseWalker] Current PauseWalker id: " + id + " Name: " + trackedPawn.Name);
                //Log.Message("[PauseWalker] Current PauseWalker id: " + id + " Dead: " + trackedPawn.Dead + " Destroyed: " + trackedPawn.Destroyed + " Discarded: " + trackedPawn.Discarded);
                //Pawn? found = Find.WorldPawns.AllPawnsAliveOrDead.FirstOrDefault(p => p.ThingID == trackedPawn.ThingID);
                //Log.Message($"[PauseWalker] Current PauseWalker in worldPawns: {found}");

                if (trackedPawn == null)
                {
                    toBeRemoved.Add(id);
                    Log.Message($"[PauseWalker] removing PauseWalker id={id}, reason: trackedPawn is null.");
                    continue;
                }
                if (!Utils.HasPauseWalkerAbility(trackedPawn))
                {
                    toBeRemoved.Add(id);
                    Log.Message($"[PauseWalker] removing PauseWalker {trackedPawn}, reason: pawn is not PauseWalker.");
                    continue;
                }
                if (!trackedPawn.IsColonist)
                {
                    toBeRemoved.Add(id);
                    Log.Message($"[PauseWalker] removing PauseWalker {trackedPawn}, reason: pawn is no longer colonist.");
                    continue;
                }
                if(PawnsFinder.All_AliveOrDead.Find(p => p.ThingID == trackedPawn.ThingID) == null)
                {
                    toBeRemoved.Add(id);
                    Log.Message($"[PauseWalker] removing PauseWalker {trackedPawn}, reason: pawn is removed from world.");
                    continue;
                }

                if (!trackedPawn.Dead && trackedPawn.IsColonist)
                {
                    if (trackedPawn.health != null && trackedPawn.health.hediffSet != null && !trackedPawn.health.hediffSet.HasHediff(PauseWalkerResurrectHediffDefOf.PauseWalkerResurrectHediff))
                    {
                        trackedPawn.health.AddHediff(PauseWalkerResurrectHediffDefOf.PauseWalkerResurrectHediff);
                    }
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

                if (!trackedPawn.Dead && KidnapUtility.IsKidnapped(trackedPawn))
                {
                    KidnapPawnReturn(trackedPawn);
                }

            }

            foreach (var id in toBeRemoved)
            {
                tracked.Remove(id);
            }


        }


        private void TryReviveThroughLetter(Pawn pawn)
        {
            if (!ChoiceLetter_PauseWalkerReturn.LetterStackExists(pawn))
            {
                Log.Message($"[PauseWalker] Try revive pawn through letter: {pawn}");
                ChoiceLetter_PauseWalkerReturn letter = ChoiceLetter_PauseWalkerReturn.MakePauseWalkerLetter(pawn);
                Find.LetterStack.ReceiveLetter(letter, null, 0, true);
            }
        }

        private void TryReviveDestroyedThroughLetter(Pawn pawn)
        {
            if (!ChoiceLetter_PauseWalkerReturn.LetterStackExists(pawn))
            {
                Log.Message($"[PauseWalker] Try revive destroyed pawn through letter: {pawn}");
                ChoiceLetter_PauseWalkerReturn letter = ChoiceLetter_PauseWalkerReturn.MakePauseWalkerLetter(pawn);
                Find.LetterStack.ReceiveLetter(letter, null, 0, true);
            }
        }


        private void Register(Pawn pawn)
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


        private void KidnapPawnReturn(Pawn pawn)
        {
            List<Faction> allFactionsListForReading = Find.FactionManager.AllFactionsListForReading;
            foreach (var item in allFactionsListForReading)
            {
                if (item.kidnapped.KidnappedPawnsListForReading.Contains(pawn))
                {
                    item.kidnapped.RemoveKidnappedPawn(pawn);
                    RecallKidnappedPawnToMap(pawn);
                }
            }
        }

        private void RecallKidnappedPawnToMap(Pawn pawn)
        {
            if (pawn.Spawned)
                return;

            // 设置为玩家派系
            if (pawn.Faction != Faction.OfPlayer)
            {
                pawn.SetFaction(Faction.OfPlayer);
            }

            Log.Message($"[PauseWalker] recall kidnapped pawn: {pawn}");

            TryReviveThroughLetter(pawn);
        }


    }
}
