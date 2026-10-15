using UnityEngine;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using HarmonyLib;
using System.Linq;

namespace Psychology.Harmony;

// Abandoning a settlement should remove its mayor.
// NOTE: the method has no [HarmonyPostfix] attribute and isn't named Postfix, so PatchAll ignores it and this has
// never run; abandoned colonies keep their mayor entry until the next load (BuildMayorDictionary).
[HarmonyPatch(typeof(MapParent),nameof(MapParent.Abandon))]
public static class MapParent_Abandon_Patch
{
    public static void Abandon(MapParent __instance)
    {
        PsycheHelper.GameComp.RemoveMayorOfThisColony(__instance.Map.Tile);
    }
}


