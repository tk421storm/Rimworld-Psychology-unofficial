using HarmonyLib;
using Verse;
using RimWorld;
using System;
using System.Reflection;
using System.Diagnostics;

namespace Psychology.Harmony;

// Applies all of Psychology's Harmony patches at startup.
//   1. Unpatches seven vanilla methods that Psychology replaces outright (romance selection weight/success chance,
//      BreakLoverAndFianceRelations, lover generation chance, GetLovinMtbHours, IdeoTrackerTickInterval,
//      SecondaryLovinChanceFactor).
//      NOTE: Unpatch(method, HarmonyPatchType.All) defaults to harmonyID "*", so this removes EVERY mod's patches on
//      those methods, not just ours. Mods that patched them before this static constructor ran are silently undone.
//   2. With the Kinsey system on, applies the manual Kinsey patches (ManualPatches.KinseyEnabledPatches).
//   3. PatchAll() for every attribute-based patch. PatchAll only picks up methods marked [HarmonyPrefix]/[HarmonyPostfix]/...
//      or named Prefix/Postfix/Transpiler/...; a few classes in Harmony/ have neither and are silently inactive (see the
//      notes on MapParent_Abandon_Patch and the ConvertIdeoAttempt patches).
[StaticConstructorOnStartup]
public static class HarmonyInitialize
{
  static HarmonyInitialize()
  {
    HarmonyLib.Harmony harmonyInstance = new HarmonyLib.Harmony("Community.Psychology.UnofficialUpdate");

    Stopwatch stopwatch = new Stopwatch();
    stopwatch.Start();

    // It might be a bit extreme to undo all other patches...
    try
    {
      harmonyInstance.Unpatch(AccessTools.Method(typeof(InteractionWorker_RomanceAttempt), nameof(InteractionWorker_RomanceAttempt.RandomSelectionWeight)), HarmonyPatchType.All);
    }
    catch (Exception ex)
    {
      Log.Error("InteractionWorker_RomanceAttempt.RandomSelectionWeight unpatch, " + ex);
    }

    try
    {
      harmonyInstance.Unpatch(AccessTools.Method(typeof(InteractionWorker_RomanceAttempt), nameof(InteractionWorker_RomanceAttempt.SuccessChance)), HarmonyPatchType.All);
    }
    catch (Exception ex)
    {
      Log.Error("InteractionWorker_RomanceAttempt.SuccessChance unpatch, " + ex);
    }

    try
    {
      harmonyInstance.Unpatch(AccessTools.Method(typeof(InteractionWorker_RomanceAttempt), "BreakLoverAndFianceRelations"), HarmonyPatchType.All);
    }
    catch (Exception ex)
    {
      Log.Error("InteractionWorker_RomanceAttempt.BreakLoverAndFianceRelations unpatch, " + ex);
    }

    try
    {
      harmonyInstance.Unpatch(AccessTools.Method(typeof(LovePartnerRelationUtility), nameof(LovePartnerRelationUtility.LovePartnerRelationGenerationChance)), HarmonyPatchType.All);
    }
    catch (Exception ex)
    {
      Log.Error("LovePartnerRelationUtility.LovePartnerRelationGenerationChance unpatch, " + ex);
    }

    try
    {
      harmonyInstance.Unpatch(AccessTools.Method(typeof(LovePartnerRelationUtility), nameof(LovePartnerRelationUtility.GetLovinMtbHours)), HarmonyPatchType.All);
    }
    catch (Exception ex)
    {
      Log.Error("LovePartnerRelationUtility.GetLovinMtbHours unpatch, " + ex);
    }

    try
    {
      harmonyInstance.Unpatch(AccessTools.Method(typeof(Pawn_IdeoTracker), nameof(Pawn_IdeoTracker.IdeoTrackerTickInterval)), HarmonyPatchType.All);
    }
    catch (Exception ex)
    {
      Log.Error("Pawn_IdeoTracker.IdeoTrackerTick unpatch, " + ex);
    }

    try
    {
      harmonyInstance.Unpatch(AccessTools.Method(typeof(Pawn_RelationsTracker), nameof(Pawn_RelationsTracker.SecondaryLovinChanceFactor)), HarmonyPatchType.All);
    }
    catch (Exception ex)
    {
      Log.Error("Pawn_RelationsTracker.SecondaryLovinChanceFactor unpatch, exception = " + ex);
    }

    
    
    Log.Message("Time for all patches: " + stopwatch.Elapsed.TotalMilliseconds);

    if (PsychologySettings.enableKinsey)
    {
      ManualPatches.KinseyEnabledPatches(harmonyInstance);
    }
    Log.Message("Psychology: implemented all vanilla patches");

    harmonyInstance.PatchAll();
    stopwatch.Stop();
  }
}

// Patches that only make sense with the Kinsey system on: they swap vanilla's orientation-trait checks for Kinsey-based
// ones (trait gain, parent generation, sibling divorce, Word of Love, AttractedToGender).
public class ManualPatches
{
  public static void KinseyEnabledPatches(HarmonyLib.Harmony harmonyInstance)
  {
    MethodInfo originalInfo;
    HarmonyMethod harmonyMethod;
    HarmonyMethod harmonyMethod2;

    try
    {
      originalInfo = AccessTools.Method(typeof(TraitSet), nameof(TraitSet.GainTrait));
      harmonyMethod = new HarmonyMethod(typeof(TraitSet_ManualPatches), nameof(TraitSet_ManualPatches.GainTrait_KinseyEnabledPrefix));
      harmonyInstance.Patch(originalInfo, prefix: harmonyMethod);
    }
    catch (Exception ex)
    {
      Log.Error("Psychology: failed to patch TraitSet.GainTrait, " + ex);
    }

    try
    {
      originalInfo = AccessTools.Method(typeof(TraitSet), nameof(TraitSet.HasTrait), new Type[] { typeof(TraitDef) });
      harmonyMethod = new HarmonyMethod(typeof(TraitSet_ManualPatches), nameof(TraitSet_ManualPatches.HasTrait_KinseyEnabledPostfix));
      harmonyInstance.Patch(originalInfo, postfix: harmonyMethod);
    }
    catch (Exception ex)
    {
      Log.Error("Psychology: failed to patch TraitSet.HasTrait1, " + ex);
    }

    try
    {
      originalInfo = AccessTools.Method(typeof(TraitSet), nameof(TraitSet.HasTrait), new Type[] { typeof(TraitDef), typeof(int) });
      harmonyMethod = new HarmonyMethod(typeof(TraitSet_ManualPatches), nameof(TraitSet_ManualPatches.HasTrait_KinseyEnabledPostfix2));
      harmonyInstance.Patch(originalInfo, postfix: harmonyMethod);
    }
    catch (Exception ex)
    {
      Log.Error("Psychology: failed to patch TraitSet.HasTrait2, " + ex);
    }

    try
    {
      originalInfo = AccessTools.Method(typeof(ChildRelationUtility), nameof(ChildRelationUtility.ChanceOfBecomingChildOf), new Type[] { typeof(Pawn), typeof(Pawn), typeof(Pawn), typeof(PawnGenerationRequest?), typeof(PawnGenerationRequest?), typeof(PawnGenerationRequest?) });
      harmonyMethod = new HarmonyMethod(typeof(ChildRelationUtility_ManualPatches), nameof(ChildRelationUtility_ManualPatches.ChanceOfBecomingChildOf_Transpiler));
      harmonyInstance.Patch(originalInfo, transpiler: harmonyMethod);
    }
    catch (Exception ex)
    {
      Log.Error("Psychology: failed to patch ChildRelationUtility.ChanceOfBecomingChildOf, " + ex);
    }

    try
    {
      originalInfo = AccessTools.Method(typeof(PawnRelationWorker_Sibling), nameof(PawnRelationWorker_Sibling.CreateRelation));
      harmonyMethod = new HarmonyMethod(typeof(PawnRelationWorker_Sibling_ManualPatches), nameof(PawnRelationWorker_Sibling_ManualPatches.CreateRelation_Transpiler));
      harmonyInstance.Patch(originalInfo, transpiler: harmonyMethod);
    }
    catch (Exception ex)
    {
      Log.Error("Psychology: failed to patch PawnRelationWorker_Sibling.CreateRelation, " + ex);
    }

    try
    {
      originalInfo = AccessTools.Method(typeof(CompAbilityEffect_WordOfLove), nameof(CompAbilityEffect_WordOfLove.ValidateTarget));
      harmonyMethod = new HarmonyMethod(typeof(CompAbilityEffect_WordOfLove_KinseyEnabledPatches), nameof(CompAbilityEffect_WordOfLove_KinseyEnabledPatches.ValidateTarget_Transpiler));
      harmonyMethod2 = new HarmonyMethod(typeof(CompAbilityEffect_WordOfLove_KinseyEnabledPatches), nameof(CompAbilityEffect_WordOfLove_KinseyEnabledPatches.ValidateTarget_Postfix));
      harmonyInstance.Patch(originalInfo, transpiler: harmonyMethod, postfix: harmonyMethod2);
    }
    catch (Exception ex)
    {
      Log.Error("Psychology: failed to patch CompAbilityEffect_WordOfLove.ValidateTarget, " + ex);
    }

    try
    {
      originalInfo = AccessTools.Method(typeof(CompAbilityEffect_WordOfLove), nameof(CompAbilityEffect_WordOfLove.Valid), new Type[] { typeof(LocalTargetInfo), typeof(bool) });
      harmonyMethod = new HarmonyMethod(typeof(CompAbilityEffect_WordOfLove_KinseyEnabledPatches), nameof(CompAbilityEffect_WordOfLove_KinseyEnabledPatches.Valid_Transpiler));
      harmonyInstance.Patch(originalInfo, transpiler: harmonyMethod);
    }
    catch (Exception ex)
    {
      Log.Error("Psychology: failed to patch CompAbilityEffect_WordOfLove.Valid, " + ex);
    }

    try
    {
      originalInfo = AccessTools.Method(typeof(RelationsUtility), nameof(RelationsUtility.AttractedToGender));
      harmonyMethod = new HarmonyMethod(typeof(RelationsUtility_KinseyEnabledPatches), nameof(RelationsUtility_KinseyEnabledPatches.AttractedToGender_KinseyEnabled_Prefix));
      harmonyInstance.Patch(originalInfo, prefix: harmonyMethod);
    }
    catch (Exception ex)
    {
      Log.Error("Psychology: failed to patch RelationsUtility.AttractedToGender, " + ex);
    }
  }
}
