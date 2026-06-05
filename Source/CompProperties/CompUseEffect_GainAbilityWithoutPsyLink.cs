using RimWorld;
using Verse;

namespace PauseWalker.CompProperties
{

    public class CompUseEffect_GainAbilityWithoutPsyLink : CompUseEffect_GainAbility
    {
        public new CompProperties_UseEffect_GainAbilityWithoutPsyLink Props
        {
            get
            {
                return (CompProperties_UseEffect_GainAbilityWithoutPsyLink)this.props;
            }
        }

        public override AcceptanceReport CanBeUsedBy(Pawn p)
        {
            if (p.abilities != null && p.abilities.abilities.Any((Ability a) => a.def == this.Props.ability))
            {
                return "PsycastNeurotrainerAbilityAlreadyLearned".Translate(p.Named("USER"), this.Props.ability.LabelCap);
            }
            return true;
        }

        public override TaggedString ConfirmMessage(Pawn p)
        {
            return null;
        }
    }
}
