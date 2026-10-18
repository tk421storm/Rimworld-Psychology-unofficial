using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using UnityEngine;

namespace Psychology
{
    // Courtship: asking out someone who isn't a partner (RomanceUtility.GetRomanticIntent == Courtship). Weight: the base
    // courtship setting x attraction x opinion x romantic drive x confidence x (1 + 0.5 per good date with them) x the
    // cheat chance if the asker has a partner. They don't ask again while "turned down my date" lasts. A yes plans a date
    // through the same scheduling as date night (RendezvousUtility.PlanDate); how the date went is decided in LordJob_Date.
    public class InteractionWorker_AskOutOnDate : InteractionWorker_DateBase
    {
        protected override RomanticIntent Intent => RomanticIntent.Courtship;

        protected override float CooldownDays => 3f;

        public override float RandomSelectionWeight(Pawn initiator, Pawn recipient)
        {
            if (PsychologySettings.courtshipChance <= 0f || !CanPlanDate(initiator, recipient))
            {
                return 0f;
            }
            if (RomanceUtility.CountMemoriesOf(initiator, recipient, ThoughtDefOfPsychology.RebuffedMyDateRequest) > 0)
            {
                return 0f;
            }
            CompPsychology comp = PsycheHelper.Comp(initiator);
            float attraction = Mathf.InverseLerp(RomanceUtility.MinAttraction, 1f, initiator.relations.SecondaryRomanceChanceFactor(recipient));
            float opinion = 0.2f + 0.8f * Mathf.InverseLerp(PsychologySettings.romanceOpinionThreshold, 100f, initiator.relations.OpinionOf(recipient));
            float drive = comp.Sexuality.AdjustedRomanticDrive;
            float confidence = 0.5f + comp.Psyche.GetPersonalityRating(PersonalityNodeDefOf.Confident);
            float history = 1f + 0.5f * RomanceUtility.GoodDateCount(initiator, recipient);
            float cheat = RomanceUtility.CheatFactor(initiator, recipient, out _);
            return PsychologySettings.courtshipChance * attraction * opinion * drive * confidence * history * cheat * RendezvousUtility.ColonySizeFactor(initiator);
        }

        public override void Interacted(Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks, out string letterText, out string letterLabel, out LetterDef letterDef, out LookTargets lookTargets)
        {
            letterText = null;
            letterLabel = null;
            letterDef = null;
            lookTargets = null;
            if (Rand.Chance(AcceptChance(recipient, initiator)))
            {
                extraSentencePacks.Add(RulePackDefOfPsychology.DateAccepted);
                RendezvousUtility.PlanDate(initiator, recipient, true);
            }
            else
            {
                extraSentencePacks.Add(RulePackDefOfPsychology.DateDeclined);
                initiator.needs?.mood?.thoughts?.memories?.TryGainMemory(ThoughtDefOfPsychology.RebuffedMyDateRequest, recipient);
            }
        }

        // The answer. The recipient must have a psyche, be old enough to date and find the asker attractive (MinAttraction).
        // Chance = (0.3 + 0.7 x attraction) x (0.4 + 0.6 x opinion from -20 to 80) x (1 + 0.5 x compatibility)
        // x (1 + 0.25 per good date with them) x the cheat chance if the recipient has a partner. Easier than a romance
        // attempt: it's only a date.
        public static float AcceptChance(Pawn recipient, Pawn initiator)
        {
            if (!PsycheHelper.PsychologyEnabled(recipient) || !SpeciesHelper.RomanceEnabled(recipient, true))
            {
                return 0f;
            }
            float romanceFactor = recipient.relations.SecondaryRomanceChanceFactor(initiator);
            if (romanceFactor < RomanceUtility.MinAttraction)
            {
                return 0f;
            }
            float attraction = 0.3f + 0.7f * Mathf.InverseLerp(RomanceUtility.MinAttraction, 1f, romanceFactor);
            float opinion = 0.4f + 0.6f * Mathf.InverseLerp(-20f, 80f, recipient.relations.OpinionOf(initiator));
            float compatibility = 1f + 0.5f * RomanceUtility.Compatibility(recipient, initiator);
            float history = 1f + 0.25f * RomanceUtility.GoodDateCount(recipient, initiator);
            float cheat = RomanceUtility.CheatFactor(recipient, initiator, out _);
            return Mathf.Clamp01(attraction * opinion * compatibility * history * cheat);
        }
    }
}
