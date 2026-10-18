using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace Psychology
{
    // A date one pawn has planned (RendezvousUtility.PlanDate). Waits until the planned day and hour, then starts
    // LordJob_Date if both are free (not downed, starving, bleeding, exhausted, working, drafted, or on another map). If
    // only one is awake the other "missed the date"; if either isn't free, both get "date cancelled". A date night is
    // dropped if they stop being partners; a courtship date only if the other pawn is gone.
    public class Hediff_PlannedDate : Hediff
    {
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref this.partner, "partner");
            Scribe_Values.Look(ref this.hour, "hour", 0);
            Scribe_Values.Look(ref this.day, "day", 0);
            Scribe_Values.Look(ref this.courtship, "courtship", false);
        }


        public override void Tick()
        {
            base.Tick();
            if(this.partner == null || this.partner.Dead || (!this.courtship && !LovePartnerRelationUtility.LovePartnerRelationExists(this.pawn, this.partner)))
            {
                PsychologyDebug.Log($"Planned date of {this.pawn} with {this.partner} dropped (partner gone, or no longer partners)");
                this.pawn.health.RemoveHediff(this);
            }
            else if(Find.TickManager.TicksAbs >= this.day && GenLocalDate.HourOfDay(this.pawn) == this.hour)
            {
                if(ShouldStartDate(pawn, partner) && ShouldStartDate(partner, pawn))
                {
                    // Check who's awake before stopping their jobs: stopping a sleeping pawn's job wakes them up, which
                    // used to make every sleeper "turn up" (and "missed our date" never happen).
                    bool pawnAwake = pawn.Awake();
                    bool partnerAwake = partner.Awake();
                    PsychologyDebug.Log($"{(this.courtship ? "Date" : "Date night")} of {this.pawn} and {this.partner}: {(pawnAwake && partnerAwake ? "starting" : "missed (one of them is asleep)")}");
                    if (pawnAwake && partnerAwake)
                    {
                        pawn.jobs.StopAll();
                        partner.jobs.StopAll();
                        if(pawn.GetLord() != null)
                        {
                            pawn.GetLord().Notify_PawnLost(pawn, PawnLostCondition.ForcedToJoinOtherLord);
                        }
                        if (partner.GetLord() != null)
                        {
                            partner.GetLord().Notify_PawnLost(partner, PawnLostCondition.ForcedToJoinOtherLord);
                        }
                        LordMaker.MakeNewLord(this.pawn.Faction, new LordJob_Date(this.pawn, this.partner, this.courtship), this.pawn.Map, new Pawn[] { this.pawn, this.partner });
                    }
                    else if(pawnAwake)
                    {
                        this.pawn.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOfPsychology.MissedDate, this.partner);
                    }
                    else if (partnerAwake)
                    {
                        this.partner.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOfPsychology.MissedDate, this.pawn);
                    }
                }
                else
                {
                    PsychologyDebug.Log($"{(this.courtship ? "Date" : "Date night")} of {this.pawn} and {this.partner} cancelled (one of them isn't free)");
                    this.pawn.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOfPsychology.DateCancelled);
                    this.partner.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOfPsychology.DateCancelled);
                }
                this.pawn.health.RemoveHediff(this);
            }
        }

        // Dev romance menu: start on the next tick.
        public void StartNow()
        {
            this.day = Find.TickManager.TicksAbs;
            this.hour = GenLocalDate.HourOfDay(this.pawn);
        }

        private static bool ShouldStartDate(Pawn p, Pawn partner)
        {
            return !p.Downed && (p.needs == null || !p.needs.food.Starving)
                && p.health.hediffSet.BleedRateTotal <= 0f
                && p.needs.rest.CurCategory < RestCategory.Exhausted
                && !p.InAggroMentalState && !p.IsPrisoner
                && p.GetTimeAssignment() != TimeAssignmentDefOf.Work
                && !p.Drafted && p.Map == partner.Map;
        }

        public Pawn partner;
        public int hour;
        public int day;
        public bool courtship;
    }
}
