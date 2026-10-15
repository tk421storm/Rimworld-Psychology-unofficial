using UnityEngine;
using RimWorld;
using Verse;
using System;
using Verse.Sound;
using HarmonyLib;
using System.Reflection;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Linq;

namespace Psychology;

/// <summary>
/// Humanoid Alien Races compatibility (loaded only with HAR, see LoadFolders.xml). Fills in two empty hooks in the main
/// assembly: SpeciesHelper.AlienRaceHeuristicSettingsHook (psyche on/off from HAR's IsSentient; no age-gap preferences
/// for ageless or non-flesh races) and RomanceUtility.AlienRaceAgeCurveHook (the race's own lovin-interval-by-age curve).
/// BUG (pre-existing, shipped in 1.6): [StaticConstructorOnStartup] only runs STATIC constructors, but the patching
/// code is in an instance constructor that nothing calls, so these patches are probably never applied and alien races
/// get the generic defaults. Please confirm in-game (no "Community.Psychology.UnofficialUpdate.AlienRace" patches in
/// the Harmony log).
/// </summary>
[StaticConstructorOnStartup]
public class AlienRace_Patches
{
    public AlienRace_Patches()
    {
        MethodInfo originalInfo;
        HarmonyMethod harmonyMethod;
        HarmonyLib.Harmony harmonyInstance = new HarmonyLib.Harmony("Community.Psychology.UnofficialUpdate.AlienRace");

        originalInfo = AccessTools.Method(typeof(SpeciesHelper), nameof(SpeciesHelper.AlienRaceHeuristicSettingsHook));
        harmonyMethod = new HarmonyMethod(typeof(AlienRace_Patches), nameof(AlienRace_Patches.AlienRaceHeuristicSettingsPostfix));
        harmonyInstance.Patch(originalInfo, postfix: harmonyMethod);

        originalInfo = AccessTools.Method(typeof(RomanceUtility), nameof(RomanceUtility.AlienRaceAgeCurveHook));
        harmonyMethod = new HarmonyMethod(typeof(AlienRace_Patches), nameof(AlienRace_Patches.AlienRaceAgeCurvePostfix));
        harmonyInstance.Patch(originalInfo, postfix: harmonyMethod);
    }

    public static void AlienRaceHeuristicSettingsPostfix(SpeciesSettings settings, ThingDef def)
    {
        if (def is AlienRace.ThingDef_AlienRace alienDef)
        {
            if (alienDef?.alienRace?.compatibility?.IsSentient is bool isSentient)
            {
                settings.enablePsyche = isSentient;
            }

            bool? immuneToAge = alienDef?.alienRace?.generalSettings?.immuneToAge;
            bool? notFlesh = !alienDef?.alienRace?.compatibility?.IsFlesh;
            if (immuneToAge == true || notFlesh == true)
            {
                settings.enableAgeGap = false;
            }
        }
    }

    // BUG (pre-existing): __result isn't taken by ref, so these assignments don't change the hook's return value. This
    // would need to be `ref SimpleCurve __result` to work, even once the patch is applied.
    public static void AlienRaceAgeCurvePostfix(SimpleCurve __result, Pawn pawn)
    {
        if (pawn?.def is AlienRace.ThingDef_AlienRace alienDef && alienDef?.alienRace?.generalSettings?.lovinIntervalHoursFromAge is SimpleCurve curve)
        {
            __result = curve;
        }
        else
        {
            __result = null;
        }
    }

}

