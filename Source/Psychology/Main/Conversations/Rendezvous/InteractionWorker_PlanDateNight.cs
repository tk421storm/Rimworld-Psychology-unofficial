using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using Verse.AI.Group;
using UnityEngine;

namespace Psychology
{
    // Shared gates for both date interactions (InteractionWorker_PlanDateNight for couples, InteractionWorker_AskOutOnDate
    // for courtship): colonists with a psyche, safe conditions, neither busy with a gathering, neither with a date within
    // the cooldown, and RomanceUtility.GetRomanticIntent must be the subclass's intent.
    public abstract class InteractionWorker_DateBase : InteractionWorker
    {
        protected abstract RomanticIntent Intent { get; }

        // No new date for either pawn until this many days after their last planned one.
        protected abstract float CooldownDays { get; }

        protected bool CanPlanDate(Pawn initiator, Pawn recipient)
        {
            return BlockReason(initiator, recipient) == null;
        }

        // Why this date can't be planned right now (English, for the dev romance menu), or null if it can.
        public string BlockReason(Pawn initiator, Pawn recipient)
        {
            if (!initiator.IsColonist || !recipient.IsColonist)
            {
                return "not both colonists";
            }
            if (!PsycheHelper.PsychologyEnabled(initiator) || !PsycheHelper.PsychologyEnabled(recipient))
            {
                return "no psyche";
            }
            if (!RendezvousUtility.AcceptableGameConditionsToStartHangingOut(initiator.Map))
            {
                return "not safe to go out (danger, injuries, under 4 colonists or half drafted)";
            }
            // lastDateTick is the planned date as an absolute tick (TicksAbs, see RendezvousUtility.PlanDate).
            int cooldownStart = Find.TickManager.TicksAbs - Mathf.RoundToInt(GenDate.TicksPerDay * CooldownDays);
            if (PsycheHelper.Comp(initiator).Psyche.lastDateTick >= cooldownStart || PsycheHelper.Comp(recipient).Psyche.lastDateTick >= cooldownStart)
            {
                return $"a date within the last {CooldownDays:0} days (or one planned)";
            }
            if (!GatheringsUtility.ShouldGuestKeepAttendingGathering(initiator) || !GatheringsUtility.ShouldGuestKeepAttendingGathering(recipient))
            {
                return "one of them can't attend (needs, mental state, etc.)";
            }
            if (RomanceUtility.GetRomanticIntent(initiator, recipient) != Intent)
            {
                return "wrong romantic intent";
            }
            return null;
        }
    }

    // Lovers plan a date night: likelier the less they currently like each other (to repair things) and the more Romantic
    // and less Independent both are. At most one date per couple per week.
    public class InteractionWorker_PlanDateNight : InteractionWorker_DateBase
    {
        protected override RomanticIntent Intent => RomanticIntent.DateNight;

        protected override float CooldownDays => 7f;

        public override float RandomSelectionWeight(Pawn initiator, Pawn recipient)
        {
            if (!CanPlanDate(initiator, recipient))
            {
                return 0f;
            }
            return 1.2f * (1f + Mathf.InverseLerp(100f, 0f,initiator.needs.mood.thoughts.TotalOpinionOffset(recipient))) * PsycheHelper.Comp(initiator).Psyche.GetPersonalityRating(PersonalityNodeDefOf.Romantic) * (1f - PsycheHelper.Comp(initiator).Psyche.GetPersonalityRating(PersonalityNodeDefOf.Independent))
                * PsycheHelper.Comp(recipient).Psyche.GetPersonalityRating(PersonalityNodeDefOf.Romantic) * (1f - PsycheHelper.Comp(recipient).Psyche.GetPersonalityRating(PersonalityNodeDefOf.Independent)) * RendezvousUtility.ColonySizeFactor(initiator);
        }


        public override void Interacted(Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks, out string letterText, out string letterLabel, out LetterDef letterDef, out LookTargets lookTargets)
        {
            // The base call clears the outs and lets other mods' hooks on InteractionWorker.Interacted see this interaction
            // (Intimacy only raises its need there).
            base.Interacted(initiator, recipient, extraSentencePacks, out letterText, out letterLabel, out letterDef, out lookTargets);
            PsychologyDebug.Log($"{initiator} plans a date night with {recipient}");
            RendezvousUtility.PlanDate(initiator, recipient, false);
        }
    }
}
