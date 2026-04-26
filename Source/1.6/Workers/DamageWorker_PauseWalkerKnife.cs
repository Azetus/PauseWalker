using System.Reflection;
using Verse;

namespace PauseWalker.Workers
{
    public class DamageWorker_PauseWalkerKnife : DamageWorker_Stab
    {
        public override DamageResult Apply(DamageInfo dinfo, Thing victim)
        {
            FieldInfo field = typeof(DamageInfo).GetField("ignoreArmorInt", BindingFlags.NonPublic | BindingFlags.Instance);

            if (field != null)
            {
                field.SetValueDirect(__makeref(dinfo), true);
            }
            return base.Apply(dinfo, victim);

        }
    }
}
