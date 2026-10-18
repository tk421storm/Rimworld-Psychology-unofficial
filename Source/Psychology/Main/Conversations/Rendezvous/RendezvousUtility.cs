using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using UnityEngine;

namespace Psychology
{
    // Shared rules for social outings: a colony-size factor (fewer than 8 colonists -> rarer), schedule preferences per
    // hour, and "is it safe to go out" (no danger, nobody bleeding, at least 4 colonists, under half drafted).
    public static class RendezvousUtility
    {
        public static float ColonySizeFactor(Pawn pawn)
        {
            return Mathf.Clamp01(pawn.Map.mapPawns.FreeColonistsCount / 8f);
        }

        public static float TimeAssignmentFactor(Pawn pawn, int hour)
        {
            if(pawn.timetable.GetAssignment(hour) == TimeAssignmentDefOf.Joy)
            {
                return 1.25f;
            }
            if (pawn.timetable.GetAssignment(hour) == TimeAssignmentDefOf.Sleep)
            {
                return 0.9f;
            }
            if (pawn.timetable.GetAssignment(hour) == TimeAssignmentDefOf.Anything)
            {
                return 0.33f;
            }
            return 0f;
        }

        
        public static bool AcceptableGameConditionsToStartHangingOut(Map map)
        {
            if (GatheringsUtility.AnyLordJobPreventsNewGatherings(map))
            {
                return false;
            }
            if (map.dangerWatcher.DangerRating != StoryDanger.None)
            {
                return false;
            }
            int freeColonistsSpawnedCount = map.mapPawns.FreeColonistsSpawnedCount;
            if (freeColonistsSpawnedCount < 4)
            {
                return false;
            }
            int num = 0;
            foreach (Pawn current in map.mapPawns.FreeColonistsSpawned)
            {
                if (current.health.hediffSet.BleedRateTotal > 0f)
                {
                    return false;
                }
                if (current.Drafted)
                {
                    num++;
                }
            }
            if ((float)num / (float)freeColonistsSpawnedCount >= 0.5f)
            {
                return false;
            }
            return true;
        }

        // Schedules a date (date night for a couple, or a courtship date) on the initiator as a Hediff_PlannedDate, and
        // sends the letter if date letters are on.
        public static void PlanDate(Pawn initiator, Pawn recipient, bool courtship)
        {
            //Choose a time that works with their schedule, based on their personality
            // Pick the hour: each node with preferredDateHours votes for its hours, weighted by how far both partners are from
            // average in that node (and damped by Aggressive), then filtered by both schedules. Less Spontaneous couples plan
            // further ahead (up to 5 days).
            Dictionary<int, float> possibleHours = new Dictionary<int, float>();
            for (int i = 0; i < GenDate.HoursPerDay; i++)
            {
                possibleHours.Add(i, 0f);
            }
            foreach (PersonalityNodeDef d in DefDatabase<PersonalityNodeDef>.AllDefsListForReading)
            {
                if(d.preferredDateHours != null)
                {
                    foreach (int h in d.preferredDateHours)
                    {
                        possibleHours[h] += (Mathf.Pow(Mathf.Abs(0.5f - PsycheHelper.Comp(initiator).Psyche.GetPersonalityRating(d)), 2) / (1.3f - PsycheHelper.Comp(initiator).Psyche.GetPersonalityRating(PersonalityNodeDefOf.Aggressive)) + Mathf.Pow(Mathf.Abs(0.5f - PsycheHelper.Comp(recipient).Psyche.GetPersonalityRating(d)), 2) / (1.3f - PsycheHelper.Comp(recipient).Psyche.GetPersonalityRating(PersonalityNodeDefOf.Aggressive)) / 2f);
                    }
                }
            }
            int hour = possibleHours.Keys.RandomElementByWeight(h => possibleHours[h] * RendezvousUtility.TimeAssignmentFactor(initiator, h) * RendezvousUtility.TimeAssignmentFactor(recipient, h));
            //More Spontaneous couples will plan their dates sooner; possibly even immediately!
            int day = Find.TickManager.TicksAbs + Mathf.RoundToInt(GenDate.TicksPerDay * 5 * (((1f - PsycheHelper.Comp(initiator).Psyche.GetPersonalityRating(PersonalityNodeDefOf.Spontaneous)) + (1f - PsycheHelper.Comp(recipient).Psyche.GetPersonalityRating(PersonalityNodeDefOf.Spontaneous))) / 2f));
            Hediff_PlannedDate plannedDate = HediffMaker.MakeHediff(HediffDefOfPsychology.PlannedDate, initiator) as Hediff_PlannedDate;
            plannedDate.partner = recipient;
            plannedDate.day = day;
            plannedDate.hour = hour;
            plannedDate.courtship = courtship;
            initiator.health.AddHediff(plannedDate);
            PsycheHelper.Comp(initiator).Psyche.lastDateTick = day;
            PsycheHelper.Comp(recipient).Psyche.lastDateTick = day;
            PsychologyDebug.Log($"{(courtship ? "Date" : "Date night")} planned: {initiator} and {recipient}, in {(day - Find.TickManager.TicksAbs) / (float)GenDate.TicksPerDay:0.0} days at hour {hour}");
            if(PsychologySettings.enableDateLetters)
            {
                int hourDiscrepancy = GenDate.HourOfDay(day, Find.WorldGrid.LongLatOf(initiator.Map.Tile).x) - hour;
                int accurateTime = day + (Math.Abs(hourDiscrepancy) * GenDate.TicksPerHour);
                String dateTime = GenDate.QuadrumDateStringAt(accurateTime, Find.WorldGrid.LongLatOf(initiator.Map.Tile).x);
                string labelKey = courtship ? "LetterLabelCourtshipDatePlanned" : "LetterLabelDatePlanned";
                string textKey = courtship ? "LetterCourtshipDatePlanned" : "LetterDatePlanned";
                Letter dateLetter = LetterMaker.MakeLetter(labelKey.Translate(), textKey.Translate(initiator, recipient, dateTime, hour), LetterDefOf.PositiveEvent, new LookTargets(initiator, recipient));
                Find.LetterStack.ReceiveLetter(dateLetter);
            }
        }
    }
}
