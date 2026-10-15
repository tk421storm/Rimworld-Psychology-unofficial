using System;
using Verse;
using Verse.AI;
using RimWorld;
using UnityEngine;

namespace Psychology;

public class Hediff_Anxiety : HediffWithComps
{
    /// <summary>
    /// Anxiety (the mod's PTSD-like condition, usually gained from mental breaks, see MentalBreaker_AnxietyPatch). Its
    /// severity also drags the LaidBack node down (Pawn_PsycheTracker.AdjustForCircumstance).
    /// 20 times a day (outside a 1-day cooldown after each attack) it rolls the current stage's mentalBreakMtbDays; on a hit
    /// while the pawn is ASLEEP they get a nightmare and wake into a panic attack. Awake pawns are not affected by this
    /// roll. Treated by the CureAnxiety operation (Recipe_CureAnxiety); tending reduces how often attacks happen.
    /// </summary>
    public const int intervalsPerDay = 20;
    public const int ticksPerInterval = GenDate.TicksPerDay / intervalsPerDay;
    public int ticksPerIntervalTracker = Mathf.CeilToInt(ticksPerInterval * Rand.Value);
    public int cooldownIntervalTracker = 0;

    public override void Tick()
    {
        base.Tick();
        if (ticksPerIntervalTracker > 0)
        {
            ticksPerIntervalTracker--;
            return;
        }
        ticksPerIntervalTracker = ticksPerInterval;
        if (cooldownIntervalTracker > 0)
        {
            cooldownIntervalTracker--;
            return;
        }
        if (pawn.InMentalState == true)
        {
            return;
        }
        float mtbDays = CurStage.mentalBreakMtbDays;
        if (mtbDays <= 0f)
        {
            return;
        }
        if (Rand.MTBEventOccurs(mtbDays, GenDate.TicksPerDay, ticksPerInterval) != true)
        {
            return;
        }
        if (pawn.jobs.curDriver.asleep)
        {
            pawn.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOfPsychology.DreamNightmare);
            pawn.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOfPsychology.PanicAttack, forceWake: true);
        }
    }
}
