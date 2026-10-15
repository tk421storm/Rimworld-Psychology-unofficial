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
    // Makes sure the race of any pawn whose health card is opened has Psychology's treatment operations.
    // NOTE: runs every time the card is drawn, edits the race's cached AllRecipes list in place and assigns it to
    // def.recipes. It also adds CureAnxiety unconditionally, whereas SpeciesHelper only adds it when Anxiety is enabled.
    // Consider moving this to SpeciesHelper's one-time def setup.
    [HarmonyPatch(typeof(HealthCardUtility), nameof(HealthCardUtility.DrawPawnHealthCard))]
    public static class HealthCardUtility_DrawPawnHealthCardPatch
    {
        [HarmonyPrefix]
        public static void AddPsychologyRecipes(ref Pawn pawn)
        {
            List<RecipeDef> recipes = pawn.def.AllRecipes;
            if (!recipes.Contains(RecipeDefOfPsychology.TreatChemicalInterest))
            {
                recipes.Add(RecipeDefOfPsychology.TreatChemicalInterest);
            }
            if (!recipes.Contains(RecipeDefOfPsychology.TreatChemicalFascination))
            {
                recipes.Add(RecipeDefOfPsychology.TreatChemicalFascination);
            }
            if (!recipes.Contains(RecipeDefOfPsychology.TreatDepression))
            {
                recipes.Add(RecipeDefOfPsychology.TreatDepression);
            }
            if (!recipes.Contains(RecipeDefOfPsychology.TreatInsomnia))
            {
                recipes.Add(RecipeDefOfPsychology.TreatInsomnia);
            }
            if (!recipes.Contains(RecipeDefOfPsychology.CureAnxiety))
            {
                recipes.Add(RecipeDefOfPsychology.CureAnxiety);
            }
            if (!recipes.Contains(RecipeDefOfPsychology.TreatPyromania))
            {
                recipes.Add(RecipeDefOfPsychology.TreatPyromania);
            }

            pawn.def.recipes = recipes;

        }

    }

}
