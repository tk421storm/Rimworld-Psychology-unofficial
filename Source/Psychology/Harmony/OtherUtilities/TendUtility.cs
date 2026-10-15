using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;
using RimWorld;
using HarmonyLib;

namespace Psychology.Harmony
{
    // Bleeding Heart: tending someone gives the doctor a small happy thought.
    // NOTE: doctor.needs.mood can be null (e.g. Biotech mechanoid tenders), which would throw here.
    [HarmonyPatch(typeof(TendUtility), nameof(TendUtility.DoTend))]
    public static class TendUtility_TendPatch
    {
        [HarmonyPostfix]
        public static void BleedingHeartThought(Pawn doctor, Pawn patient)
        {
            if (doctor != null)
            {
                doctor.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOfPsychology.DoctorBleedingHeart, patient);
            }
        }
    }
}
