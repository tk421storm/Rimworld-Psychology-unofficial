using System;
using Verse;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;

namespace Psychology.Harmony;

// Banishing a mayor (or a prisoner/guest who somehow holds office) removes their mayorship.
// Since 1.6, PawnBanishUtility.Banish has two overloads (with and without a destination tile), so each gets its own patch.
[HarmonyPatch(typeof(PawnBanishUtility), nameof(PawnBanishUtility.Banish), new Type[] { typeof(Pawn), typeof(bool) })]
public static class PawnBanishUtility_Banish_Patch
{
    [HarmonyPostfix]
    public static void Banish(Pawn pawn, bool giveThoughts = true)
    {
        if (pawn.Faction != Faction.OfPlayer && pawn.HostFaction != Faction.OfPlayer)
        {
            return;
        }
        PsycheHelper.GameComp.RemoveAllMayorshipsFromPawn(pawn);
    }
}

[HarmonyPatch(typeof(PawnBanishUtility), nameof(PawnBanishUtility.Banish), new Type[] { typeof(Pawn), typeof(PlanetTile), typeof(bool) })]
public static class PawnBanishUtility_Banish_Patch2
{
    [HarmonyPostfix]
    public static void Banish(Pawn pawn, PlanetTile tile, bool giveThoughts = true)
    {
        if (pawn.Faction != Faction.OfPlayer && pawn.HostFaction != Faction.OfPlayer)
        {
            return;
        }
        PsycheHelper.GameComp.RemoveAllMayorshipsFromPawn(pawn);
    }
}


