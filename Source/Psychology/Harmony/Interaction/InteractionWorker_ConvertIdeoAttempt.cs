using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;
using Verse;
using RimWorld;
using HarmonyLib;

namespace Psychology.Harmony
{
    // (Ideology) Meant to make Outspoken/Judgmental/Moralistic initiators proselytise more, and Trusting recipients get
    // targeted more, by a factor 3^(sum - 2).
    // NOTE: neither patch in this file has a [HarmonyPostfix] attribute and neither method is named Postfix, so PatchAll
    // ignores them; they have never been active. They also lack PsychologyEnabled checks, so add those before enabling.
    [HarmonyPatch(typeof(InteractionWorker_ConvertIdeoAttempt), nameof(InteractionWorker_ConvertIdeoAttempt.RandomSelectionWeight))]
    public static class InteractionWorker_ConvertIdeoAttempt_RandomSelectionWeight_Patch
    {
        public static void RandomSelectionWeight(ref float __result, Pawn initiator, Pawn recipient)
        {
            float initOutspoken = PsycheHelper.Comp(initiator).Psyche.GetPersonalityRating(PersonalityNodeDefOf.Outspoken);
            float initJudgmental = PsycheHelper.Comp(initiator).Psyche.GetPersonalityRating(PersonalityNodeDefOf.Judgmental);
            float initMoralistic = PsycheHelper.Comp(initiator).Psyche.GetPersonalityRating(PersonalityNodeDefOf.Moralistic);
            float reciTrusting = PsycheHelper.Comp(recipient).Psyche.GetPersonalityRating(PersonalityNodeDefOf.Trusting);
            float additiveFactor = initOutspoken + initJudgmental + initMoralistic + reciTrusting - 2f;
            __result *= Mathf.Pow(3f, additiveFactor);
        }
    }

    // Meant to scale the certainty lost per conversion attempt by 3^(initiator Cool + recipient Naive - Nostalgic - Experimental).
    // (Inactive, see above.)
    [HarmonyPatch(typeof(InteractionWorker_ConvertIdeoAttempt), nameof(InteractionWorker_ConvertIdeoAttempt.CertaintyReduction))]
    public static class InteractionWorker_ConvertIdeoAttempt_CertaintyReduction_Patch
    {
        public static void CertaintyReduction(ref float __result, Pawn initiator, Pawn recipient)
        {
            float initCool = PsycheHelper.Comp(initiator).Psyche.GetPersonalityRating(PersonalityNodeDefOf.Cool);
            float reciNaive = PsycheHelper.Comp(recipient).Psyche.GetPersonalityRating(PersonalityNodeDefOf.Naive);
            float reciNostalgic = PsycheHelper.Comp(recipient).Psyche.GetPersonalityRating(PersonalityNodeDefOf.Nostalgic);
            float reciExperimental = PsycheHelper.Comp(recipient).Psyche.GetPersonalityRating(PersonalityNodeDefOf.Experimental);
            float additiveFactor = initCool + reciNaive - reciNostalgic - reciExperimental;
            __result *= Mathf.Pow(3f, additiveFactor);
        }
    }
}

