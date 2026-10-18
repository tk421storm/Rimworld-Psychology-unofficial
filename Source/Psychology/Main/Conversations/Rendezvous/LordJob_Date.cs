using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;
using Verse.Grammar;
using UnityEngine;

namespace Psychology
{
    // A 4-6 hour date. Ends early on danger, injury, drafting, work time or exhaustion. Finishing a date night gives both
    // a "went on a date" memory; finishing a courtship date gives each a good or bad date memory scaled by how it went
    // for them (RomanceUtility.DateQuality), and if it went well for both it may end in a romance attempt.
    class LordJob_Date : LordJob
    {
        public LordJob_Date()
        { }

        public LordJob_Date(Pawn initiator, Pawn recipient, bool courtship)
        {
            this.initiator = initiator;
            this.recipient = recipient;
            this.courtship = courtship;
        }

        public override StateGraph CreateGraph()
        {
            StateGraph stateGraph = new StateGraph();
            LordToil_HangOut lordToil_HangOut = new LordToil_HangOut(new Pawn[] { initiator, recipient });
            stateGraph.AddToil(lordToil_HangOut);
            LordToil_End lordToil_End = new LordToil_End();
            stateGraph.AddToil(lordToil_End);
            Transition transition = new Transition(lordToil_HangOut, lordToil_End);
            transition.AddTrigger(new Trigger_TickCondition(() => this.ShouldBeCalledOff()));
            transition.AddTrigger(new Trigger_TickCondition(() => this.initiator.health.summaryHealth.SummaryHealthPercent < 1f || this.recipient.health.summaryHealth.SummaryHealthPercent < 1f));
            transition.AddTrigger(new Trigger_TickCondition(() => this.initiator.Drafted || this.recipient.Drafted));
            transition.AddTrigger(new Trigger_PawnLost());
            stateGraph.AddTransition(transition);
            this.timeoutTrigger = new Trigger_TicksPassed(Rand.RangeInclusive(GenDate.TicksPerHour*4, GenDate.TicksPerHour*6));
            Transition transition2 = new Transition(lordToil_HangOut, lordToil_End);
            transition2.AddTrigger(this.timeoutTrigger);
            transition2.AddPreAction(new TransitionAction_Custom((Action)delegate
            {
                this.Finished();
            }));
            stateGraph.AddTransition(transition2);
            return stateGraph;
        }
        public void Finished()
        {
            if (this.courtship)
            {
                FinishedCourtship();
                return;
            }
            this.initiator.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOfPsychology.WentOnDate, this.recipient);
            this.recipient.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOfPsychology.WentOnDate, this.initiator);
        }

        private void FinishedCourtship()
        {
            // Count earlier good dates first, so this one doesn't count towards its own romance roll.
            float previousGoodDates = (RomanceUtility.GoodDateCount(this.initiator, this.recipient) + RomanceUtility.GoodDateCount(this.recipient, this.initiator)) / 2f;
            float initiatorQuality = RomanceUtility.DateQuality(this.initiator, this.recipient);
            float recipientQuality = RomanceUtility.DateQuality(this.recipient, this.initiator);
            RomanceUtility.GainDateMemory(this.initiator, this.recipient, initiatorQuality);
            RomanceUtility.GainDateMemory(this.recipient, this.initiator, recipientQuality);
            PsychologyDebug.Log($"Date of {this.initiator} and {this.recipient} finished: quality {initiatorQuality:0.00} / {recipientQuality:0.00} (compatibility {RomanceUtility.Compatibility(this.initiator, this.recipient):0.00}), earlier good dates {previousGoodDates:0.#}");
            if (initiatorQuality <= 0f || recipientQuality <= 0f || LovePartnerRelationUtility.LovePartnerRelationExists(this.initiator, this.recipient))
            {
                PsychologyDebug.Log("  no romance roll (the date didn't go well for both, or they're already partners)");
                return;
            }
            float chance = RomanceUtility.RomanceChanceAfterDate(this.initiator, this.recipient, previousGoodDates);
            bool roll = Rand.Chance(chance);
            PsychologyDebug.Log($"  romance chance {chance:P0} (opinions {this.initiator.relations.OpinionOf(this.recipient)} / {this.recipient.relations.OpinionOf(this.initiator)}) -> {(roll ? "romance attempt" : "not this time")}");
            if (roll)
            {
                RomanceUtility.TryFireRomanceAttempt(this.initiator, this.recipient);
            }
        }

        public override void ExposeData()
        {
            Scribe_References.Look(ref this.initiator, "initiator");
            Scribe_References.Look(ref this.recipient, "recipient");
            Scribe_Values.Look(ref this.courtship, "courtship", false);
        }
        
        public override string GetReport(Pawn pawn)
        {
            return "LordReportDate".Translate();
        }
        
        private bool ShouldBeCalledOff()
        {
            return !GatheringsUtility.AcceptableGameConditionsToContinueGathering(base.Map) || this.initiator.GetTimeAssignment() == TimeAssignmentDefOf.Work || this.recipient.GetTimeAssignment() == TimeAssignmentDefOf.Work || this.initiator.needs.rest.CurLevel < 0.3f || this.recipient.needs.rest.CurLevel < 0.3f;
        }
        
        private Trigger_TicksPassed timeoutTrigger;
        public Pawn initiator;
        public Pawn recipient;
        public bool courtship;
    }
}
