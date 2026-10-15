using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Psychology;

// Panic attack (from Anxiety): the pawn flees and cowers in place. Every 250 ticks Anxiety severity creeps up a little;
// if the anxiety is tended, the attack can end early (likelier the better the tending). Ending it starts a one-day
// cooldown before the next attack.
public class MentalState_PanicAttack : MentalState
{
    public int mentalStateTicker = 0;
    public const int mentalStateTicksPerInterval = 250;

    public override void PreStart()
    {
        if (pawn.jobs.curDriver.asleep)
        {
            pawn.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOfPsychology.DreamNightmare);
        }
        base.PreStart();
    }

    public override void PostStart(string reason)
    {
        pawn.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOfPsychology.PanicAttack);
        base.PostStart(reason);
    }

    public override void PostEnd()
    {
        pawn.needs.mood.thoughts.memories.RemoveMemoriesOfDef(ThoughtDefOfPsychology.PanicAttack);
        if (pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOfPsychology.Anxiety) is Hediff_Anxiety anxiety)
        {
            // Give at least one day between anxiety attacks
            anxiety.cooldownIntervalTracker = Hediff_Anxiety.intervalsPerDay;
        }
        base.PostEnd();
    }

    public override void MentalStateTick(int delta)
    {
        base.MentalStateTick(delta);
        if (mentalStateTicker > 0)
        {
            mentalStateTicker--;
            return;
        }
        mentalStateTicker = mentalStateTicksPerInterval;
        if (pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOfPsychology.Anxiety) is Hediff_Anxiety anxiety)
        {
            
            anxiety.Severity += 0.00000002f * mentalStateTicksPerInterval;
            if (anxiety.IsTended() == true)
            {
                HediffComp_TendDuration tendDuration = anxiety.TryGetComp<HediffComp_TendDuration>();
                float tendQuality = tendDuration.tendQuality;
                float mtbDays = 1f / Mathf.Max(0.01f, Mathf.Exp(tendQuality) - 1f) * anxiety.CurStage.mentalBreakMtbDays;
                if (Rand.MTBEventOccurs(mtbDays, GenDate.TicksPerDay, mentalStateTicksPerInterval) != true)
                {
                    return;
                }
                RecoverFromState();
            }
        }
        if (!pawn.Spawned || pawn.Downed) // || !pawn.RaceProps.Humanlike
        {
            return;
        }
        if (pawn.jobs.curJob.def != JobDefOf.FleeAndCower)
        {
            pawn.jobs.StartJob(new Job(JobDefOf.FleeAndCower, pawn.Position), JobCondition.InterruptForced, null, false, true, null);
        }
    }
}