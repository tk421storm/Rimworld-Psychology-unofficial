using System;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using RimWorld;
using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;

namespace Psychology.Harmony;

// Same for getting married: old break-up/rejection thoughts between the couple are cleared via RomanceHelperMethods.
[HarmonyPatch(typeof(MarriageCeremonyUtility), nameof(MarriageCeremonyUtility.Married))]
public static class MarriageCeremonyUtility_MarriedPatch
{
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
    {
        return RomanceHelperMethods.InterdictTryGainAndRemoveMemories(codes);
    }
}

