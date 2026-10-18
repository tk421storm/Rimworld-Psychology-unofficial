using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using UnityEngine;
using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;

namespace Psychology.Harmony;

// Proposal acceptance: Codependent recipients always say yes; otherwise vanilla's chance is shifted by
// 0.8 x (Romantic + Pure - 1) using relativistic addition in the -1..1 space (romantic, traditional pawns accept more).
// With Kinsey on, the recipient's age-adjusted romantic drive shifts it again by 0.4 x (drive - 0.7), capped at +/-0.3:
// neutral at a typical drive of 0.7, about +14 points at a 50% base for a very high drive, about -12 for a very low one.
[HarmonyPatch(typeof(InteractionWorker_MarriageProposal), nameof(InteractionWorker_MarriageProposal.AcceptanceChance))]
public static class InteractionWorker_MarriageProposal_AcceptanceChancePatch
{
    [HarmonyPostfix]
    public static void PsychologyException(InteractionWorker_MarriageProposal __instance, Pawn initiator, Pawn recipient, ref float __result)
    {
        if (PsycheHelper.PsychologyEnabled(recipient) != true)
        {
            __result = 0f;
            return;
        }
        if (!SpeciesHelper.RomanceEnabled(initiator, false) || !SpeciesHelper.RomanceEnabled(recipient, false))
        {
            __result = 0f;
            return;
        }
        if (recipient.story.traits.HasTrait(TraitDefOfPsychology.Codependent))
        {
            // Codependent pawns will always accept a marriage proposal
            __result = 1f;
            return;
        }
        
        CompPsychology recipientComp = PsycheHelper.Comp(recipient);
        float recipientRomanatic = recipientComp.Psyche.GetPersonalityRating(PersonalityNodeDefOf.Romantic);
        float recipientPure = recipientComp.Psyche.GetPersonalityRating(PersonalityNodeDefOf.Pure);

        float x = Mathf.Clamp(-1f + recipientRomanatic + recipientPure, -0.999f, 0.999f);
        x *= 0.8f;
        float num = Mathf.Clamp(-1f + 2f * __result, -0.999f, 0.999f);
        num = PsycheHelper.RelativisticAddition(num, x);

        if (PsychologySettings.enableKinsey)
        {
            float driveShift = Mathf.Clamp(0.4f * (recipientComp.Sexuality.AdjustedRomanticDrive - 0.7f), -0.3f, 0.3f);
            num = PsycheHelper.RelativisticAddition(num, driveShift);
        }
        __result = Mathf.Clamp01(0.5f * (1f + num));
    }
}

// DISABLED since 1.5 (dropped from the 1.5/1.6 builds), presumably because vanilla's Interacted() changed and the
// transpiler no longer finds its TryGainMemory(RejectedMyProposal/IRejectedTheirProposal) calls. As a result a rejected
// proposal gives vanilla's fixed thoughts instead of the personality-scaled ones from RomanceHelperMethods.
// TODO: verify against the 1.6/1.7 IL and re-enable.
#if false
[HarmonyPatch(typeof(InteractionWorker_MarriageProposal), nameof(InteractionWorker_MarriageProposal.Interacted))]
public static class InteractionWorker_MarriageProposal_InteractedPatch
{
    // TryGain RejectedMyProposal
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
    {
        return RomanceHelperMethods.InterdictTryGainAndRemoveMemories(codes);
    }
}
#endif
