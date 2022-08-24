﻿using System;
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

    [HarmonyPatch(typeof(InteractionWorker_RomanceAttempt), nameof(InteractionWorker_RomanceAttempt.RandomSelectionWeight))]
    public static class InteractionWorker_RomanceAttempt_SelectionWeightPatch
    {
        //[LogPerformance]
        [HarmonyPriority(Priority.Last)]
        [HarmonyPostfix]
        public static void PsychologyException(ref float __result, Pawn initiator, Pawn recipient)
        {
            // Keep Vanilla in these cases
            if (TutorSystem.TutorialMode
                || LovePartnerRelationUtility.LovePartnerRelationExists(initiator, recipient)
                || !PsycheHelper.PsychologyEnabled(initiator))
            {
                return;
            }
            // Codependents won't romance anyone if they are in a relationship
            if (LovePartnerRelationUtility.HasAnyLovePartner(initiator) && initiator.story.traits.HasTrait(TraitDefOfPsychology.Codependent))
            {
                __result = 0f;
                return;
            }
            //Don't hit on people in mental breaks... unless you're really freaky.
            float initiatorExperimental = PsycheHelper.Comp(initiator).Psyche.GetPersonalityRating(PersonalityNodeDefOf.Experimental);
            bool initiatorLecher = initiator.story.traits.HasTrait(TraitDefOfPsychology.Lecher);
            if (recipient.InMentalState && initiatorExperimental < 0.8f && !initiatorLecher)
            {
                __result = 0f;
                return;
            }
            /* ROMANCE CHANCE FACTOR INCLUDES THE FOLLOWING: */
            /* - SEXUAL PREFERENCE FACTOR */
            /* - AGE FACTOR */
            /* - OTHER PAWN BEAUTY FACTOR */
            /* - PAWN SEX AND ROMANCE DRIVE FACTORS */
            /* - INCEST FACTOR */
            /* - PSYCHIC LOVE SPELL FACTOR */
            float romChance = initiator.relations.SecondaryRomanceChanceFactor(recipient);
            if (romChance < 0.15f)
            {
                __result = 0f;
                return;
            }
            float romChanceMult = Mathf.InverseLerp(0.15f, 1f, romChance);

            /* INITIATOR OPINION FACTOR */
            float initiatorRomantic = PsycheHelper.Comp(initiator).Psyche.GetPersonalityRating(PersonalityNodeDefOf.Romantic);
            float initiatorOpinion = (float)initiator.relations.OpinionOf(recipient);
            float recipientOpinion = (float)recipient.relations.OpinionOf(initiator);
            float initiatorOpinMult;
            //Only lechers will romance someone that has less than base opinion of them
            if (!initiatorLecher)
            {
                if (Mathf.Max(initiatorOpinion, recipientOpinion) < PsychologyBase.RomanceThreshold())
                {
                    __result = 0f;
                    return;
                }
                // Romantic pawns are more responsive to their opinion of the recipient
                float x = Mathf.InverseLerp(PsychologyBase.RomanceThreshold(), 100f, initiatorOpinion);
                initiatorOpinMult = 0.5f * Mathf.Pow(2f * x, 2f * initiatorRomantic + 1e-5f);
            }
            else
            {
                // Lechers are more frisky
                initiatorOpinMult = 1.5f;
            }

            /* GET INITIATOR PERSONALITY VALUES */
            float initiatorAggressive = PsycheHelper.Comp(initiator).Psyche.GetPersonalityRating(PersonalityNodeDefOf.Aggressive);
            float initiatorConfident = PsycheHelper.Comp(initiator).Psyche.GetPersonalityRating(PersonalityNodeDefOf.Confident);

            //float initiatorOpenMinded = initiator.story.traits.HasTrait(TraitDefOfPsychology.OpenMinded) ? 1f : 0f;

            /* INITIATOR EXISTING LOVE PARTNER FACTOR */
            //A pawn with high enough opinion of their lover will not hit on other pawns unless they are lecherous or polygamous (and their lover is also polygamous).
            float existingPartnerMult = 1f;
            if (!new HistoryEvent(initiator.GetHistoryEventForLoveRelationCountPlusOne(), initiator.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo())
            {
                Pawn pawn = LovePartnerRelationUtility.ExistingMostLikedLovePartner(initiator, allowDead: false);
                if (pawn != null && !initiatorLecher && !initiator.story.traits.HasTrait(TraitDefOfPsychology.Polygamous)
                    && !pawn.story.traits.HasTrait(TraitDefOfPsychology.Polygamous))
                {
                    float opinionOfLover = (float)initiator.relations.OpinionOf(pawn);
                    // More romantic pawns will stay loyal longer, but will also turn faster against a partner they don't like
                    float romDisp = 2f * initiatorRomantic - 1f;
                    float maxOpinionOfLover = 50f - 40f * romDisp;
                    float minOpinionOfLover = -50f + 25f * romDisp;
                    existingPartnerMult = Mathf.InverseLerp(maxOpinionOfLover, minOpinionOfLover, opinionOfLover);
                }
            }

            /* INITIATOR KNOWN SEXUALITY FACTOR */
            float knownSexFactor = 1f;
            float straightWomanFactor = 1f;
            if (PsychologyBase.ActivateKinsey())
            {
                //People who have hit on someone in the past and been rejected because of their sexuality will rarely attempt to hit on them again.
                knownSexFactor = (PsycheHelper.Comp(initiator).Sexuality.IncompatibleSexualityKnown(recipient) && !initiatorLecher) ? 0.05f : 1f;
                // Not sure whether to keep this mechanic...
                float kinseyFactor = PsycheHelper.Comp(initiator).Sexuality.kinseyRating / 6f;
                straightWomanFactor = initiator.gender == Gender.Female ? Mathf.Lerp(0.15f, 1f, kinseyFactor) : 1f;

            }
            else
            {
                bool initiatorIsGay = initiator.story.traits.HasTrait(TraitDefOf.Gay);
                bool recipientIsGay = recipient.story.traits.HasTrait(TraitDefOf.Gay);
                if (initiator.gender == recipient.gender)
                {
                    knownSexFactor = (!initiatorIsGay || !recipientIsGay) ? 0.15f : 1f;
                }
                else
                {
                    knownSexFactor = (initiatorIsGay || recipientIsGay) ? 0.15f : 1f;
                }
                straightWomanFactor = initiatorIsGay ? 1f : initiator.gender == Gender.Female ? 0.15f : 1f;
            }

            // Include chance multiplier from settings
            __result = 1.15f * PsychologyBase.RomanceChance() * straightWomanFactor * romChanceMult * initiatorOpinMult * existingPartnerMult * knownSexFactor;

            // Confident pawns are more likely to make a move on attractive mates
            float chanceCutOff = 0.75f;
            float confidenceFactor = initiatorConfident + initiatorAggressive;
            __result = __result < chanceCutOff ? __result : chanceCutOff + confidenceFactor * (__result - chanceCutOff);

            return;
        }
    }

    [HarmonyPatch(typeof(InteractionWorker_RomanceAttempt), nameof(InteractionWorker_RomanceAttempt.SuccessChance))]
    public static class InteractionWorker_RomanceAttempt_SuccessChancePatch
    {
        //[LogPerformance]
        [HarmonyPriority(Priority.Last)]
        [HarmonyPostfix]
        public static void NewSuccessChance(ref float __result, Pawn initiator, Pawn recipient)
        {
            /* Throw out the result and replace it with our own formula. */
            if (!PsycheHelper.PsychologyEnabled(initiator))
            {
                return;
            }
            // Codependents won't romance anyone if they are in a relationship
            bool recipientCodependent = recipient.story.traits.HasTrait(TraitDefOfPsychology.Codependent);
            if (LovePartnerRelationUtility.HasAnyLovePartner(recipient) && recipientCodependent)
            {
                __result = 0f;
                return;
            }
            float successChance = 0.6f;
            
            /* ROMANCE CHANCE FACTOR INCLUDES THE FOLLOWING: */
            /* SEXUAL PREFERENCE FACTOR */
            /* AGE FACTOR */
            /* OTHER PAWN BEAUTY FACTOR */
            /* PAWN SEX AND ROMANCE DRIVE FACTORS */
            /* DISABILITY FACTOR */
            /* PSYCHIC LOVE SPELL FACTOR */
            successChance *= recipient.relations.SecondaryRomanceChanceFactor(initiator);

            /* RECIPIENT OPINION FACTOR */
            float recipientRomantic = PsycheHelper.Comp(recipient).Psyche.GetPersonalityRating(PersonalityNodeDefOf.Romantic);
            bool recipientLecher = recipient.story.traits.HasTrait(TraitDefOfPsychology.Lecher);
            float recipientOpinion = (float)recipient.relations.OpinionOf(initiator);
            float opinionFactor = Mathf.InverseLerp(PsychologyBase.RomanceThreshold(), 100f, recipientOpinion);
            if (recipientLecher)
            {
                // Only lechers will romance someone that they have a low opinion of
                opinionFactor = 0.5f + Mathf.Sqrt(opinionFactor);
            }
            // More romantic recipients have higher standards but respond more strongly to overtures from high opinion initiators
            successChance *= 0.5f * Mathf.Pow(2f * opinionFactor, 2f * recipientRomantic + 1e-5f);

            /* EXISTING LOVE PARTNER FACTOR */
            if (!new HistoryEvent(recipient.GetHistoryEventForLoveRelationCountPlusOne(), recipient.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo() && !recipient.story.traits.HasTrait(TraitDefOfPsychology.Polygamous))
            {
                float existingLovePartnerMult = 1f;
                Pawn pawn = null;
                if (recipient.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Lover, (Pawn x) => !x.Dead) != null)
                {
                    pawn = recipient.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Lover, null);
                    //existingLovePartnerMult = recipientCodependent ? 0.01f : 0.6f;
                    existingLovePartnerMult = 0.6f;
                }
                else if (recipient.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Fiance, (Pawn x) => !x.Dead) != null)
                {
                    pawn = recipient.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Fiance, null);
                    //existingLovePartnerMult = recipientCodependent ? 0.01f : 0.1f;
                    existingLovePartnerMult = 0.1f;
                }
                else if (recipient.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Spouse, (Pawn x) => !x.Dead) != null)
                {
                    pawn = recipient.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Spouse, null);
                    //existingLovePartnerMult = recipientCodependent ? 0.01f : 0.3f;
                    existingLovePartnerMult = 0.3f;
                }
                if (pawn != null)
                {
                    float opinionOfPartner = (float)recipient.relations.OpinionOf(pawn);
                    float romDisp = 2f * recipientRomantic - 1f;
                    // More romantic pawns stay loyal for longer
                    float opinionOfPartnerMult = romDisp > 0 ? Mathf.InverseLerp(100f - 75f * romDisp, 0, opinionOfPartner) : Mathf.InverseLerp(100f, 75f * romDisp, opinionOfPartner);
                    float romChanceMult = Mathf.Clamp01(1f - recipient.relations.SecondaryRomanceChanceFactor(pawn));
                    // Modified the formula so that opinion still matters even with maximum romance chance factor
                    // Being romantically compatible will keep pawns more faithful, but this weakens with lower opinion
                    existingLovePartnerMult *= 0.5f * (opinionOfPartnerMult + romChanceMult);
                }
                // Lechers won't care as much about their partners and are more promiscuous in general
                if (recipientLecher)
                {
                    existingLovePartnerMult = 0.5f + Mathf.Sqrt(existingLovePartnerMult);
                }
                successChance *= existingLovePartnerMult;
            }
            // Account for user setting of romance chance
            successChance *= PsychologyBase.RomanceChance();
            // Clamp to keep the chance between 0 and 1
            __result = Mathf.Clamp01(successChance);
        }
    }

    [HarmonyPatch(typeof(InteractionWorker_RomanceAttempt), "TryAddCheaterThought")]
    public static class InteractionWorker_RomanceAttempt_CheaterThoughtPatch
    {
        //[LogPerformance]
        [HarmonyPostfix]
        public static void AddCodependentThought(Pawn pawn, Pawn cheater)
        {
            pawn.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOfPsychology.CheatedOnMeCodependent, cheater);
        }
    }

    [HarmonyPatch(typeof(InteractionWorker_RomanceAttempt), "BreakLoverAndFianceRelations")]
    public static class InteractionWorker_RomanceAttempt_BreakRelationsPatch
    {
        //[LogPerformance]
        [HarmonyPrefix]
        public static bool BreakRelations(Pawn pawn, ref List<Pawn> oldLoversAndFiances)
        {
            oldLoversAndFiances = new List<Pawn>();
            while (true)
            {
                Pawn firstDirectRelationPawn = pawn.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Lover, null);
                if (firstDirectRelationPawn != null && (!firstDirectRelationPawn.story.traits.HasTrait(TraitDefOfPsychology.Polygamous) || !pawn.story.traits.HasTrait(TraitDefOfPsychology.Polygamous)))
                {
                    pawn.relations.RemoveDirectRelation(PawnRelationDefOf.Lover, firstDirectRelationPawn);
                    Pawn recipient = firstDirectRelationPawn;
                    if (PsycheHelper.PsychologyEnabled(pawn) && PsycheHelper.PsychologyEnabled(recipient))
                    {
                        BreakupHelperMethods.AddExLover(pawn, recipient);
                        BreakupHelperMethods.AddExLover(recipient, pawn);
                        BreakupHelperMethods.AddBrokeUpOpinion(recipient, pawn);
                        BreakupHelperMethods.AddBrokeUpMood(recipient, pawn);
                        BreakupHelperMethods.AddBrokeUpMood(pawn, recipient);
                    }
                    else
                    {
                        pawn.relations.AddDirectRelation(PawnRelationDefOf.ExLover, firstDirectRelationPawn);
                    }
                    oldLoversAndFiances.Add(firstDirectRelationPawn);
                }
                else
                {
                    Pawn firstDirectRelationPawn2 = pawn.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Fiance, null);
                    if (firstDirectRelationPawn2 == null)
                    {
                        break;
                    }
                    else if (!firstDirectRelationPawn2.story.traits.HasTrait(TraitDefOfPsychology.Polygamous) || !pawn.story.traits.HasTrait(TraitDefOfPsychology.Polygamous))
                    {
                        pawn.relations.RemoveDirectRelation(PawnRelationDefOf.Fiance, firstDirectRelationPawn2);
                        Pawn recipient2 = firstDirectRelationPawn2;
                        if (PsycheHelper.PsychologyEnabled(pawn) && PsycheHelper.PsychologyEnabled(recipient2))
                        {
                            BreakupHelperMethods.AddExLover(pawn, recipient2);
                            BreakupHelperMethods.AddExLover(recipient2, pawn);
                            BreakupHelperMethods.AddBrokeUpOpinion(recipient2, pawn);
                            BreakupHelperMethods.AddBrokeUpMood(recipient2, pawn);
                            BreakupHelperMethods.AddBrokeUpMood(pawn, recipient2);
                        }
                        else
                        {
                            pawn.relations.AddDirectRelation(PawnRelationDefOf.ExLover, firstDirectRelationPawn2);
                        }
                        oldLoversAndFiances.Add(firstDirectRelationPawn2);
                    }
                }
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(InteractionWorker_RomanceAttempt), nameof(InteractionWorker_RomanceAttempt.Interacted))]
    public static class InteractionWorker_RomanceAttempt_InteractedLearnSexualityPatch
    {
        //[LogPerformance]
        [HarmonyPriority(Priority.High)]
        [HarmonyPrefix]
        public static bool LearnSexuality(Pawn initiator, Pawn recipient)
        {
            if (PsycheHelper.PsychologyEnabled(initiator) && PsycheHelper.PsychologyEnabled(recipient) && PsychologyBase.ActivateKinsey())
            {
                PsycheHelper.Comp(initiator).Sexuality.LearnSexuality(recipient);
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(InteractionWorker_RomanceAttempt), nameof(InteractionWorker_RomanceAttempt.Interacted))]
    public static class InteractionWorker_RomanceAttempt_InteractedHandleThoughtsPatch
    {
        //[LogPerformance]
        [HarmonyPostfix]
        public static void HandleNewThoughts(InteractionWorker_RomanceAttempt __instance, Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks, string letterText, string letterLabel, LetterDef letterDef)
        {
            if (extraSentencePacks.Contains(RulePackDefOf.Sentence_RomanceAttemptAccepted))
            {
                foreach (ThoughtDef d in (from tgt in initiator.needs.mood.thoughts.memories.Memories
                                          where tgt.def.defName.Contains("BrokeUpWithMe")
                                          select tgt.def))
                {
                    initiator.needs.mood.thoughts.memories.RemoveMemoriesOfDefWhereOtherPawnIs(d, recipient);
                }
                foreach (ThoughtDef d in (from tgt in recipient.needs.mood.thoughts.memories.Memories
                                          where tgt.def.defName.Contains("BrokeUpWithMe")
                                          select tgt.def))
                {
                    recipient.needs.mood.thoughts.memories.RemoveMemoriesOfDefWhereOtherPawnIs(d, initiator);
                }
                initiator.needs.mood.thoughts.memories.RemoveMemoriesOfDefWhereOtherPawnIs(ThoughtDefOfPsychology.BrokeUpWithMeCodependent, recipient);
                recipient.needs.mood.thoughts.memories.RemoveMemoriesOfDefWhereOtherPawnIs(ThoughtDefOfPsychology.BrokeUpWithMeCodependent, initiator);
            }
            else if (extraSentencePacks.Contains(RulePackDefOf.Sentence_RomanceAttemptRejected))
            {
                if (initiator.story.traits.HasTrait(TraitDefOfPsychology.Lecher))
                {
                    initiator.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOfPsychology.RebuffedMyRomanceAttemptLecher, recipient);
                }
            }
        }
    }

}