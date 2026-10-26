using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace Psychology
{
  /// <summary>
  /// What a vanilla romance attempt turns into; see RomanceAttemptRouter.GetAttemptType.
  /// </summary>
  public enum RomanceAttemptType
  {
    Direct,     // vanilla's "become lovers" roll: the end of a good date, a player-ordered romance, or nothing else fits
    DateNight,  // they're already partners: plan a date night
    Courtship,  // ask them out on a date
    Hookup,     // proposition them for a casual hookup
    Cheating    // an affair: a hookup behind a partner's back (one who would mind)
  }

  /// <summary>
  /// Routes vanilla romance attempts (InteractionDefOf.RomanceAttempt, picked by the social interaction system, the
  /// Histrionic advance job or other mods) through Psychology's romance: RomanticIntent decides between a date night,
  /// courtship and a hookup, and a hookup that would be cheating becomes an affair. The attempt is resolved on the spot
  /// so the social log entry carries its type and result; see InteractionWorker_RomanceAttempt_Interacted_Patches.
  /// </summary>
  public static class RomanceAttemptRouter
  {
    // Set while a romance attempt should run vanilla's lovers roll instead of being routed; the text is the reason
    // (RomanceUtility.TryFireRomanceAttempt at the end of a good date).
    public static string directReason;

    public static RomanceAttemptType GetAttemptType(Pawn initiator, Pawn recipient, out Pawn cheatOn, out string reason)
    {
      cheatOn = null;
      if (directReason != null)
      {
        reason = directReason;
        return RomanceAttemptType.Direct;
      }
      if (initiator.CurJob?.def == JobDefOf.TryRomance)
      {
        reason = "ordered by the player";
        return RomanceAttemptType.Direct;
      }
      RomanticIntent intent = RomanceUtility.GetRomanticIntent(initiator, recipient, out reason);
      switch (intent)
      {
        case RomanticIntent.DateNight:
          return RomanceAttemptType.DateNight;
        case RomanticIntent.None:
          return RomanceAttemptType.Direct;
      }
      RomanceUtility.CheatFactor(initiator, recipient, out cheatOn);
      if (cheatOn != null && RomanceUtility.WillPawnTryHookup(initiator))
      {
        // The cheat chance was already rolled into the romance attempt's selection weight.
        reason += $", cheating on {cheatOn.LabelShort}";
        return RomanceAttemptType.Cheating;
      }
      return intent == RomanticIntent.Courtship ? RomanceAttemptType.Courtship : RomanceAttemptType.Hookup;
    }

    // Why the routed attempt can't happen right now (English, for the debug log), or null if it can. A blocked attempt
    // gets no selection weight. Direct attempts are never blocked here.
    public static string BlockReason(RomanceAttemptType type, Pawn initiator, Pawn recipient)
    {
      switch (type)
      {
        case RomanceAttemptType.DateNight:
          return ((InteractionWorker_DateBase)InteractionDefOfPsychology.PlanDate.Worker).BlockReason(initiator, recipient);
        case RomanceAttemptType.Courtship:
          if (PsychologySettings.courtshipChance <= 0f)
          {
            return "courtship is off";
          }
          if (RomanceUtility.CountMemoriesOf(initiator, recipient, ThoughtDefOfPsychology.RebuffedMyDateRequest) > 0)
          {
            return "they turned this pawn's date request down recently";
          }
          return ((InteractionWorker_DateBase)InteractionDefOfPsychology.AskOutOnDate.Worker).BlockReason(initiator, recipient);
        case RomanceAttemptType.Hookup:
        case RomanceAttemptType.Cheating:
          if (OnVisitDuty(initiator) || OnVisitDuty(recipient))
          {
            return "visitor on a duty (trading, visiting, raiding or a quest)";
          }
          if (RomanceUtility.CountMemoriesOf(initiator, recipient, ThoughtDefOfPsychology.RebuffedMyHookupAttempt) > 0)
          {
            return "they turned this pawn's hookup down recently";
          }
          if (!new HistoryEvent(HistoryEventDefOf.GotLovin_NonSpouse, initiator.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo()
              && !initiator.relations.DirectRelationExists(PawnRelationDefOf.Spouse, recipient))
          {
            return "ideoligion forbids lovin outside marriage";
          }
          if (!RomanceUtility.IsPawnFree(initiator))
          {
            return "busy (needs, danger, drafted or an important job)";
          }
          return null;
        default:
          return null;
      }
    }

    // A non-colonist following one of vanilla's group orders (a trade caravan, a visit, a raid or siege, a quest's
    // lodgers or shuttle). Guests of mods with their own lord jobs, like Hospitality's, can still hook up.
    public static bool OnVisitDuty(Pawn pawn)
    {
      return !pawn.IsColonist && pawn.GetLord()?.LordJob?.GetType().Assembly == typeof(LordJob).Assembly;
    }

    // Selection-weight gate: false if a randomly picked attempt would be routed to something that can't happen now.
    public static bool CanRoute(Pawn initiator, Pawn recipient)
    {
      RomanceAttemptType type = GetAttemptType(initiator, recipient, out _, out _);
      return type != RomanceAttemptType.Direct && BlockReason(type, initiator, recipient) == null;
    }

    public static RulePackDef TypeSentence(RomanceAttemptType type)
    {
      switch (type)
      {
        case RomanceAttemptType.DateNight:
          return RulePackDefOfPsychology.RomanceAttemptDateNight;
        case RomanceAttemptType.Courtship:
          return RulePackDefOfPsychology.RomanceAttemptCourtship;
        case RomanceAttemptType.Hookup:
          return RulePackDefOfPsychology.RomanceAttemptHookup;
        case RomanceAttemptType.Cheating:
          return RulePackDefOfPsychology.RomanceAttemptCheating;
        default:
          return RulePackDefOfPsychology.RomanceAttemptDirect;
      }
    }

    /// <summary>
    /// Carries out a routed (non-Direct) attempt, adding the result sentence to the log entry. Returns the result and the
    /// calculations behind it (English, for the debug log).
    /// </summary>
    public static string Resolve(RomanceAttemptType type, Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks)
    {
      switch (type)
      {
        case RomanceAttemptType.DateNight:
          RendezvousUtility.PlanDate(initiator, recipient, false);
          return "date night planned";
        case RomanceAttemptType.Courtship:
          {
            bool accepted = InteractionWorker_AskOutOnDate.Ask(initiator, recipient, extraSentencePacks, out float chance, out string details);
            return $"{(accepted ? "date accepted" : "date declined")}; accept chance {chance:P0} = {details}";
          }
        default:
          return ProposeHookup(initiator, recipient, extraSentencePacks);
      }
    }

    /// <summary>
    /// Romance while socializing (Pawn_InteractionsTracker_TryInteractRandomlyPatch). Every super-active social job
    /// (relaxing socially at a table or campfire, standing at a party or Psychology hangout, Ideology dance parties and
    /// rituals with socially active spectators, billiards) gives each of the pawn's random interactions a chance, the
    /// "romance while socializing" setting, to become a look around for romance instead. A pawn looking has a need:
    /// single, or allowed another partner, or rolling their cheat chance (RomanticNeed). Candidates are the pawns
    /// close enough to interact with now, weighted by the romance attempt's selection weight (attraction, opinion,
    /// cooldowns, the router's gates), and the one picked gets a romance attempt, routed as usual. Not on a date, nor for
    /// visitors on a vanilla group order (OnVisitDuty).
    /// True if an attempt was made, which stands in for that random interaction.
    /// </summary>
    private static readonly System.Func<Pawn_InteractionsTracker, RandomSocialMode> currentSocialMode =
      AccessTools.MethodDelegate<System.Func<Pawn_InteractionsTracker, RandomSocialMode>>(
        AccessTools.PropertyGetter(typeof(Pawn_InteractionsTracker), "CurrentSocialMode"));

    public static bool TrySocialRomance(Pawn pawn)
    {
      if (PsychologySettings.socialRomanceChance <= 0f || !PsycheHelper.PsychologyEnabled(pawn))
      {
        return false;
      }
      // Vanilla's social mode: the job's, capped by the lord duty, drafting and mental states.
      if (currentSocialMode(pawn.interactions) != RandomSocialMode.SuperActive || pawn.GetLord()?.LordJob is LordJob_Date || OnVisitDuty(pawn))
      {
        return false;
      }
      if (!Rand.Chance(PsychologySettings.socialRomanceChance / 100f))
      {
        return false;
      }
      float need = RomanticNeed(pawn, out string needReason);
      if (!Rand.Chance(need))
      {
        PsychologyDebug.Log($"{pawn} ({pawn.CurJobDef?.defName}) isn't looking for romance: {needReason}");
        return false;
      }
      InteractionDef def = InteractionDefOf.RomanceAttempt;
      List<Pawn> candidates = new List<Pawn>();
      List<float> weights = new List<float>();
      foreach (Pawn p in pawn.Map.mapPawns.SpawnedPawnsInFaction(pawn.Faction))
      {
        if (p == pawn || pawn.HostileTo(p) || !SocialInteractionUtility.CanReceiveRandomInteraction(p) || !pawn.interactions.CanInteractNowWith(p, def))
        {
          continue;
        }
        float weight = def.Worker.RandomSelectionWeight(pawn, p);
        if (weight > 0f)
        {
          candidates.Add(p);
          weights.Add(weight);
        }
      }
      if (candidates.Count == 0)
      {
        PsychologyDebug.Log($"{pawn} ({pawn.CurJobDef?.defName}) looks for romance ({needReason}): no one nearby");
        return false;
      }
      int picked = candidates.Count - 1;
      float roll = Rand.Value * weights.Sum();
      for (int i = 0; i < candidates.Count; i++)
      {
        roll -= weights[i];
        if (roll < 0f)
        {
          picked = i;
          break;
        }
      }
      if (PsychologyDebug.Enabled)
      {
        string list = string.Join(", ", candidates.Select((p, i) => $"{p.LabelShort} {weights[i]:0.00}"));
        PsychologyDebug.Log($"{pawn} ({pawn.CurJobDef?.defName}) looks for romance ({needReason}): candidates {list} -> {candidates[picked].LabelShort}");
      }
      return pawn.interactions.TryInteractWith(candidates[picked], def);
    }

    // Whether a pawn is looking for romance, as a chance: 1 if they have no love partner or another one wouldn't be
    // cheating (ideoligion allows it, Polygamous, partners who don't mind); otherwise the cheat chance on the partner
    // they'd feel worst about cheating on (RomanceUtility.CheatFactor).
    public static float RomanticNeed(Pawn pawn, out string reason)
    {
      if (!LovePartnerRelationUtility.HasAnyLovePartner(pawn))
      {
        reason = "single";
        return 1f;
      }
      float chance = RomanceUtility.CheatFactor(pawn, null, out Pawn cheatOn);
      reason = cheatOn == null ? "has a partner, but it wouldn't be cheating" : $"would cheat on {cheatOn.LabelShort}, chance {chance:P0}";
      return chance;
    }

    // The hookup proposal, answered on the spot (JobDriver_LeadHookup walks over first): the recipient must be free and
    // willing, then rolls RomanceUtility.HookupAcceptChance. A yes sends both to a bed for two (JobDriver_DoLovinCasual,
    // which gives cheated-on partners their thoughts); a no gives both the same memories as a refused LeadHookup.
    private static string ProposeHookup(Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks)
    {
      float chance;
      string details;
      if (!RomanceUtility.IsPawnFree(recipient))
      {
        chance = 0f;
        details = "recipient busy";
      }
      else if (!RomanceUtility.WillPawnTryHookup(recipient))
      {
        chance = 0f;
        details = "recipient won't hook up (asexual, ideoligion, settings or age)";
      }
      else
      {
        chance = RomanceUtility.HookupAcceptChance(recipient, initiator, out details);
      }
      string calc = $"accept chance {chance:P0} = {details}";
      if (!Rand.Chance(chance))
      {
        initiator.needs?.mood?.thoughts?.memories?.TryGainMemory(ThoughtDefOfPsychology.RebuffedMyHookupAttempt, recipient);
        recipient.needs?.mood?.thoughts?.memories?.TryGainMemory(ThoughtDefOfPsychology.FailedHookupAttemptOnMe, initiator);
        extraSentencePacks.Add(RulePackDefOfPsychology.HookupFailed);
        return "hookup declined; " + calc;
      }
      Building_Bed bed = JoyGiver_CasualHookup.FindHookupBed(initiator, recipient);
      if (bed == null)
      {
        extraSentencePacks.Add(RulePackDefOfPsychology.HookupNoBed);
        return "hookup accepted, but no bed for two; " + calc;
      }
      extraSentencePacks.Add(RulePackDefOfPsychology.HookupSucceeded);
      Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf.InitiatedLovin, initiator.Named(HistoryEventArgsNames.Doer)));
      // As at the end of JobDriver_LeadHookup.
      initiator.jobs.jobQueue.EnqueueFirst(new Job(JobDefOfPsychology.DoLovinCasual, recipient, bed, bed.GetSleepingSlotPos(0)));
      recipient.jobs.jobQueue.EnqueueFirst(new Job(JobDefOfPsychology.DoLovinCasual, initiator, bed, bed.GetSleepingSlotPos(1)));
      recipient.jobs.EndCurrentJob(JobCondition.InterruptOptional);
      initiator.jobs.EndCurrentJob(JobCondition.InterruptOptional);
      return $"hookup accepted, going to {bed.LabelShort}; " + calc;
    }
  }
}
