using Verse;

namespace PauseWalker.Workers
{
    public class DamageWorker_PauseWalkerKnife : DamageWorker_Stab
    {
        public override DamageResult Apply(DamageInfo dinfo, Thing victim)
        {
            dinfo.SetIgnoreArmor(true);
            return base.Apply(dinfo, victim);

        }
    }
}
