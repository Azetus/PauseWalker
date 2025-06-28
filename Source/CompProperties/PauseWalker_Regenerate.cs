using RimWorld;
using UnityEngine;
using Verse;

namespace PauseWalker.CompProperties
{
    public class PauseWalker_Regenerate : HediffComp
    {
        private List<Hediff_Injury> tmpInjuries = new List<Hediff_Injury>();
        private List<Hediff_MissingPart> tmpMissingParts = new List<Hediff_MissingPart>();

        public PauseWalkerHediffCompProperties_Regenerate Props => (PauseWalkerHediffCompProperties_Regenerate)this.props;

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            float regenBudget = Props.healAmount;

            if (regenBudget <= 0f || Pawn?.health == null) return;

            // 疤痕
            var candidates = Pawn.health.hediffSet.hediffs
                .Where(hd => hd.IsPermanent() || hd.def.chronic)
                .ToList();

            if (candidates.TryRandomElement(out Hediff toCure))
            {
                HealthUtility.Cure(toCure);
            }

            // 伤口
            Pawn.health.hediffSet.GetHediffs<Hediff_Injury>(ref tmpInjuries, (Hediff_Injury h) => !h.IsPermanent() && h.Severity > 0f);
            foreach (var injury in tmpInjuries)
            {
                float healAmount = Mathf.Min(regenBudget, injury.Severity);
                injury.Heal(healAmount);
                regenBudget -= healAmount;
                Pawn.health.hediffSet.Notify_Regenerated(healAmount);
                if (regenBudget <= 0f) return;
            }

            // 身体部位
            Pawn.health.hediffSet.GetHediffs<Hediff_MissingPart>(ref tmpMissingParts, h =>
                h.Part.parent != null &&
                !tmpInjuries.Any(x => x.Part == h.Part.parent) &&
                Pawn.health.hediffSet.GetFirstHediffMatchingPart<Hediff_MissingPart>(h.Part.parent) == null &&
                Pawn.health.hediffSet.GetFirstHediffMatchingPart<Hediff_AddedPart>(h.Part.parent) == null
            );

            if (tmpMissingParts.Any())
            {
                Hediff_MissingPart missingPart = tmpMissingParts.First();
                BodyPartRecord part = missingPart.Part;

                Pawn.health.RemoveHediff(missingPart);

                // 移除部位缺失，添加伤口
                var added = Pawn.health.AddHediff(HediffDefOf.Misc, part);
                float partHealth = Pawn.health.hediffSet.GetPartHealth(part);
                added.Severity = Mathf.Max(partHealth - 1f, partHealth * 0.9f);

                Pawn.health.hediffSet.Notify_Regenerated(partHealth - added.Severity);
            }
        }
    }

    public class PauseWalkerHediffCompProperties_Regenerate : HediffCompProperties
    {
        public float healAmount = 100.0f;

        public PauseWalkerHediffCompProperties_Regenerate()
        {
            this.compClass = typeof(PauseWalker_Regenerate);
        }
    }
}
