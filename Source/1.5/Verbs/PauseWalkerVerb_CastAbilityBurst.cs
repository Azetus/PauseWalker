using RimWorld;

namespace PauseWalker.Verbs
{
    public class PauseWalkerVerb_CastAbilityBurst: Verb_CastAbility
    {
        protected override int ShotsPerBurst
        {
            get
            {
                return this.verbProps.burstShotCount;
            }
        }
    }
}
