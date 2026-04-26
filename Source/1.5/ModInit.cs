using Verse;
using HarmonyLib;


namespace PauseWalker
{
    public class PauseWalkerMod : Verse.Mod
    {
        public PauseWalkerMod(ModContentPack content) : base(content)
        {
            Log.Message("[PauseWalker] is loaded!");
            new Harmony("Aliza.PauseWalker").PatchAll();
        }
    }
}
