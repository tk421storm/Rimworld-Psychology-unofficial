using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using System.Linq;
using Verse.AI;
using System.Reflection.Emit;

namespace Psychology
{
  /// <summary>
  /// What a pawn wants from another pawn romantically; see RomanceUtility.GetRomanticIntent.
  /// </summary>
  public enum RomanticIntent
  {
    None,
    DateNight,  // they're already a couple (InteractionWorker_PlanDateNight)
    Courtship,  // interested in them, or has had a good date with them (InteractionWorker_AskOutOnDate)
    Hookup      // attracted to them but not interested in more (JoyGiver_CasualHookup)
  }

  /// <summary>
  /// Shared romance helpers: species age checks injected into vanilla code, Psychology's lovin frequency, and the casual
  /// hookup rules (who finds whom attractive, cheating, whether a pawn is free / willing).
  /// </summary>
  public static class RomanceUtility
  {
    public static readonly HashSet<JobDef> jobDefsThatPreventHookups = new HashSet<JobDef> { JobDefOf.LayDown, JobDefOf.BeatFire, JobDefOf.Arrest, JobDefOf.Capture, JobDefOf.EscortPrisonerToBed, JobDefOf.ExtinguishSelf, JobDefOf.FleeAndCower, JobDefOf.MarryAdjacentPawn, JobDefOf.PrisonerExecution, JobDefOf.ReleasePrisoner, JobDefOf.Rescue, JobDefOf.SocialFight, JobDefOf.SpectateCeremony, JobDefOf.TakeToBedToOperate, JobDefOf.TakeWoundedPrisonerToBed, JobDefOf.UseCommsConsole, JobDefOf.Vomit, JobDefOf.Wait_Downed, JobDefOfPsychology.DoLovinCasual, JobDefOf.Lovin, JobDefOf.EnterTransporter, JobDefOf.GiveSpeech, JobDefOf.BestowingCeremony, JobDefOf.PrepareSkylantern, JobDefOf.Deathrest, JobDefOf.Breastfeed, JobDefOfPsychology.Abuse, JobDefOfPsychology.BreakHunt };

    public static SimpleCurve VanillaLovinMTBHoursAgeCurve => (SimpleCurve)AccessTools.Field(typeof(JobDriver_Lovin), "LovinIntervalHoursFromAgeCurve").GetValue(null);

    //pawn.CurJob.def != JobDefOfPsychology.JobDateLead &&
    //pawn.CurJob.def != JobDefOfPsychology.JobDateFollow &&
    //pawn.CurJob.def != JobDefOfPsychology.JobHangoutLead &&
    //pawn.CurJob.def != JobDefOfPsychology.JobHangoutFollow &&

    // Transpiler helper: replaces vanilla's human-only constant 16 (minimum lovin age) with the species' minimum lovin age
    // ("never" if romance is off). The Log.Message calls only run once, when the patch is applied.
    public static IEnumerable<CodeInstruction> InterdictRomanceAges(IEnumerable<CodeInstruction> codes, OpCode opCodePawn)
    {
      Log.Message("InterdictRomanceAges, start");
      foreach (CodeInstruction c in codes)
      {
        if (c.operand is float f && f == 16f)
        {
          Log.Message("InterdictRomanceAges, load opCodePawn");
          yield return new CodeInstruction(opCodePawn);
          Log.Message("InterdictRomanceAges, load Ldc_I4_0");
          yield return new CodeInstruction(OpCodes.Ldc_I4_0);
          Log.Message("InterdictRomanceAges, call SpeciesSettingsMinRomanceAge");
          yield return CodeInstruction.Call(typeof(RomanceUtility), nameof(RomanceUtility.SpeciesSettingsMinRomanceAge));
        }
        else
        {
          yield return c;
        }
      }
      Log.Message("InterdictRomanceAges, end");
    }

    public static float SpeciesSettingsMinRomanceAge(Pawn pawn, bool isDating)
    {
      float bioAge = pawn.ageTracker.AgeBiologicalYearsFloat;
      if (!PsycheHelper.PsychologyEnabled(pawn))
      {
        return bioAge + 1f;
      }
      SpeciesSettings settings = SpeciesHelper.GetOrMakeSpeciesSettingsFromThingDef(pawn.def);
      float romanceAge = isDating ? settings.minDatingAge : settings.minLovinAge;
      return romanceAge < 0f ? bioAge + 1f : romanceAge;
    }

    // Replaces the 12 h base in vanilla GetLovinMtbHours. The single-pawn factors already carry the age curve in hours, so
    // the base is just the "lovin frequency" setting (2 = twice as often).
    public static float NewLovinMTBHoursScale(Pawn pawn, Pawn partner)
    {
      return 1f / PsychologySettings.lovinFrequency;
    }

    // One pawn's lovin-interval factor: 1 / (1 - pain), 1 / (2 x consciousness) below 50% consciousness, Biotech gene
    // factors, and the age/sex-drive factor. -1 (no lovin) for children, pawns without a psyche or species without romance.
    public static float LovinMtbSinglePawnFactorPsychology(Pawn pawn)
    {
      if (!PsycheHelper.PsychologyEnabled(pawn))
      {
        return -1f;
      }

      if (!SpeciesHelper.RomanceEnabled(pawn, false))
      {
        return -1f;
      }
      if (pawn?.ageTracker?.Adult != true)
      {

        // No underage lovin
        return -1f;
      }

      float num = 1f;
      num /= 1f - pawn.health.hediffSet.PainTotal;
      float level = pawn.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness);
      if (level < 0.5f)
      {
        num /= 2f * level;
      }

      if (ModsConfig.BiotechActive && pawn.genes != null)
      {
        foreach (Gene gene in pawn.genes.GenesListForReading)
        {
          num *= gene.def.lovinMTBFactor;
        }
      }

      float ageFactor = LovinMtbHoursAgeFactor(pawn);
      if (ageFactor < 0f)
      {
        return -1f;
      }
      num *= ageFactor;

      return num;
    }

    // Age and sex-drive part of one pawn's interval: the square root of their lovin curve hours, so that a couple's
    // interval (the product of both factors) is the geometric mean of their two curve values; with Kinsey, divided by
    // (sex drive / 0.8). -1 (no lovin) without a psyche, for species without lovin, or with no sex drive.
    public static float LovinMtbHoursAgeFactor(Pawn pawn)
    {
      float curveHours = LovinCurveHours(pawn);
      if (curveHours < 0f)
      {
        return -1f;
      }
      float factor = Mathf.Sqrt(curveHours);
      if (PsychologySettings.enableKinsey)
      {
        float drive = PsycheHelper.Comp(pawn).Sexuality.AdjustedSexDrive;
        if (drive <= 0f)
        {
          return -1f;
        }
        factor /= drive / 0.8f;
      }
      return factor;
    }

    // Hours from the lovin age curve for this pawn: the race's own curve (HAR) at their age, or vanilla's at their
    // human-equivalent age (scaled by the species' minimum lovin age). Species with no lovin age limit use the curve's
    // lowest (prime-age) value. -1 without a psyche or for species without lovin.
    public static float LovinCurveHours(Pawn pawn)
    {
      if (!PsycheHelper.PsychologyEnabled(pawn))
      {
        return -1f;
      }
      SpeciesSettings settings = SpeciesHelper.GetOrMakeSpeciesSettingsFromThingDef(pawn.def);
      if (settings.minLovinAge < 0f)
      {
        return -1f;
      }
      SimpleCurve curve = GetCorrectAgeCurve(pawn, out bool useVanilla);
      if (settings.minLovinAge == 0f)
      {
        return curve.Points.Min(p => p.y);
      }
      float age = pawn.ageTracker.AgeBiologicalYearsFloat;
      if (useVanilla)
      {
        age = PsycheHelper.LovinBioAgeToVanilla(age, settings.minLovinAge);
      }
      return curve.Evaluate(age);
    }

    public static SimpleCurve GetCorrectAgeCurve(Pawn pawn, out bool useVanilla)
    {
      SimpleCurve curve = AlienRaceHooks.LovinAgeCurve(pawn);
      useVanilla = curve == null;
      if (useVanilla)
      {
        curve = VanillaLovinMTBHoursAgeCurve;
      }
      return curve;
    }

    /// <summary>
    /// Builds a list of up to five other pawns that <paramref name="pawn"/> finds suitable for the given activity. Looks at romance chance factor for hookups and opinion for dates.
    /// </summary>
    /// <param name="pawn">The pawn who is looking</param>
    /// <param name="hookup">Whether this list is for a hookup or a date</param>
    /// <returns>A list of pawns, with love relations first, then descending order by the secondary factor.</returns>
    // Up to 5 candidates: existing partners (except bed-mates, for hookups) first, then the most attractive colonists above
    // the attraction cut-off, the minimum hookup opinion setting, and not having turned this pawn down before.
    public static List<Pawn> FindAttractivePawns(Pawn pawn, bool hookup = true)
    {
      List<Pawn> result = new List<Pawn>();
      //Removed asexual check, it instead goes in the joy givers that generate jobs that need this
      //Put existing partners in the list
      if (LovePartnerRelationUtility.HasAnyLovePartner(pawn))
      {
        foreach (Pawn p in GetAllLoveRelationPawns(pawn, false, true))
        {
          //Skip pawns they share a bed with except for dates
          if (!DoWeShareABed(pawn, p) || !hookup)
          {
            result.Add(p);
          }
        }
      }
      //Stop here if non-spouse lovin' is not allowed
      if (!new HistoryEvent(HistoryEventDefOf.GotLovin_NonSpouse, pawn.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo() && hookup)
      {
        return result;
      }
      //Then grab some attractive pawns
      while (result.Count < 5)
      {
        //For a hookup we need to start with a non-zero number, or pawns that they actually don't find attractive end up on the list
        //For a date we can start at zero since we're looking at opinion instead of romance factor
        float num = hookup ? 0.15f : 0f;
        Pawn tempPawn = null;
        foreach (Pawn p in pawn.Map.mapPawns.FreeColonistsSpawned)
        {
          //Skip them if they're already in the list, or they share a bed if it's a hookup
          if (result.Contains(p) || pawn == p || (DoWeShareABed(pawn, p) && hookup))
          {
            continue;
          }
          //For hookup check romance factor, for date check opinion
          else if ((pawn.relations.SecondaryRomanceChanceFactor(p) > num && hookup) || (pawn.relations.OpinionOf(p) > num && !hookup))
          {
            //This will skip people who recently turned them down
            Thought_Memory memory = pawn.needs.mood.thoughts.memories.Memories.Find(delegate (Thought_Memory x)
            {
              if (x.def == ThoughtDefOfPsychology.RebuffedMyHookupAttempt)
              {
                return x.otherPawn == p;
              }
              return false;
            });
            //Need to also check opinion against setting for a hookup
            if ((memory == null && pawn.relations.OpinionOf(p) > PsychologySettings.minOpinionForHookup) || !hookup)
            {
              num = pawn.relations.SecondaryRomanceChanceFactor(p);
              tempPawn = p;
            }

          }
        }
        if (tempPawn == null)
        {
          break;
        }
        result.Add(tempPawn);
      }

      return result;
    }

    public static bool DoWeShareABed(Pawn pawn, Pawn other)
    {
      return pawn.ownership.OwnedBed != null && pawn.ownership.OwnedBed.OwnersForReading.Contains(other);
    }

    /// <summary>
    /// Determines if an interaction between <paramref name="pawn"/> and <paramref name="target"/> would be cheating from <paramref name="pawn"/>'s point of view. Includes a list of pawns that would think they are being cheated on.
    /// </summary>
    /// <param name="pawn"></param>
    /// <param name="target"></param>
    /// <param name="cheaterList">A list of pawns who will think that <paramref name="pawn"/> cheated on them, regardless of what <paramref name="pawn"/> thinks</param>
    /// <returns>True or False</returns>
    // True if sleeping with target would count as cheating for pawn: the target isn't a partner, pawn's ideoligion doesn't
    // allow another partner, and pawn isn't Polygamous. cheaterList = the partners who would mind.
    public static bool IsThisCheating(Pawn pawn, Pawn target, out List<Pawn> cheaterList)
    {
      //This has to happen to get passed out
      cheaterList = new List<Pawn>();
      //Are they in a relationship?
      if (target != null && LovePartnerRelationUtility.LovePartnerRelationExists(pawn, target))
      {
        return false;
      }
      foreach (Pawn p in GetAllLoveRelationPawns(pawn, false, false))
      {
        if (!new HistoryEvent(pawn.GetHistoryEventForLoveRelationCountPlusOne(), p.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo() && p.CaresAboutCheating())
        {
          cheaterList.Add(p);
        }
      }
      //The cheater list is for use later, initiator will only look at their ideo and settings to decide if they're cheating
      if (new HistoryEvent(pawn.GetHistoryEventForLoveRelationCountPlusOne(), pawn.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo() || !pawn.CaresAboutCheating())
      {
        return false;
      }
      return true;
    }

    public static bool CaresAboutCheating(this Pawn pawn)
    {
      if (pawn?.story?.traits?.HasTrait(TraitDefOfPsychology.Polygamous) != false)
      {
        return false;
      }
      return true;
    }

    /// <summary>
    /// Probability (0-1) that <paramref name="pawn"/> cheats on <paramref name="partner"/>, used both for casual hookups
    /// (WillPawnContinue) and for whether a pawn with a partner makes a romance attempt on someone else
    /// (InteractionWorker_RomanceAttempt selection weight): the "cheating chance" setting x an opinion curve x personality.
    /// - Opinion: normal pawns use a Romantic-shaped curve, 0 at or above 50 - 40r and 1 at or below -50 + 25r with
    ///   r = 2 x Romantic - 1 (romantic pawns stay loyal longer but turn faster on a partner they dislike). Lechers cheat
    ///   on anyone below 70 opinion who shares their map (1 at 15), and below 100 when apart (1 at 50).
    /// - Personality: 2^((Experimental - 0.5) - (Moralistic - 0.5) - (Pure - 0.5)), about x0.35 to x2.8.
    /// Codependent pawns never cheat; a setting of 0 turns cheating off.
    /// </summary>
    public static float CheatChance(Pawn pawn, Pawn partner)
    {
      if (PsychologySettings.cheatChance <= 0f || !PsycheHelper.PsychologyEnabled(pawn) || pawn.story.traits.HasTrait(TraitDefOfPsychology.Codependent))
      {
        return 0f;
      }
      Pawn_PsycheTracker psyche = PsycheHelper.Comp(pawn).Psyche;
      float opinion = pawn.relations.OpinionOf(partner);
      float opinionFactor;
      if (pawn.story.traits.HasTrait(TraitDefOfPsychology.Lecher))
      {
        opinionFactor = pawn.Map == partner.Map ? Mathf.InverseLerp(70f, 15f, opinion) : Mathf.InverseLerp(100f, 50f, opinion);
      }
      else
      {
        float romDisp = 2f * psyche.GetPersonalityRating(PersonalityNodeDefOf.Romantic) - 1f;
        opinionFactor = Mathf.InverseLerp(50f - 40f * romDisp, -50f + 25f * romDisp, opinion);
      }
      float personality = Mathf.Pow(2f, (psyche.GetPersonalityRating(PersonalityNodeDefOf.Experimental) - 0.5f)
                                      - (psyche.GetPersonalityRating(PersonalityNodeDefOf.Moralistic) - 0.5f)
                                      - (psyche.GetPersonalityRating(PersonalityNodeDefOf.Pure) - 0.5f));
      return Mathf.Clamp01(PsychologySettings.cheatChance / 100f * opinionFactor * personality);
    }

    /// <summary>
    /// Returns true if interaction between <paramref name="pawn"/> and <paramref name="target"/> is not cheating and is allowed by ideo. Otherwise, finds the partner they would feel the worst about cheating on and decides based on opinion.
    /// </summary>
    /// <param name="pawn"></param>
    /// <param name="target"></param>
    /// <param name="cheatOn">The pawn they feel worst about cheating on</param>
    /// <returns></returns>
    // Whether a pawn goes through with a hookup that would be cheating: yes with probability CheatFactor.
    public static bool WillPawnContinue(Pawn pawn, Pawn target, out Pawn cheatOn)
    {
      float chance = CheatFactor(pawn, target, out cheatOn);
      bool goAhead = Rand.Chance(chance);
      if (cheatOn != null)
      {
        PsychologyDebug.Log($"{pawn} considers a hookup with {target}, cheating on {cheatOn}: chance {chance:P0} -> {(goAhead ? "goes ahead" : "stays faithful")}");
      }
      return goAhead;
    }

    /// <summary>
    /// Chance (0-1) that <paramref name="pawn"/> goes ahead with something romantic with <paramref name="target"/>: 1 if it
    /// isn't cheating, otherwise CheatChance for the partner they'd feel worst about cheating on (the lowest chance).
    /// </summary>
    public static float CheatFactor(Pawn pawn, Pawn target, out Pawn cheatOn)
    {
      cheatOn = null;
      //Pawn may think they are cheating even though no partners will be upset (e.g. the no spouses mod); that's allowed
      if (!IsThisCheating(pawn, target, out List<Pawn> cheatedOnList) || cheatedOnList.NullOrEmpty())
      {
        return 1f;
      }
      float chance = float.MaxValue;
      foreach (Pawn p in cheatedOnList)
      {
        float partnerChance = CheatChance(pawn, p);
        if (partnerChance < chance)
        {
          chance = partnerChance;
          cheatOn = p;
        }
      }
      return chance;
    }

    // Minimum romance chance factor (attraction) for any romantic interest; the same cut-off vanilla romance uses.
    public const float MinAttraction = 0.15f;

    /// <summary>
    /// The one place that decides what <paramref name="initiator"/> wants from <paramref name="recipient"/>, so date night,
    /// courtship and hookups share the same gates. Both need a psyche and must be old enough to date. Partners get
    /// DateNight. Otherwise the initiator must find the recipient attractive (MinAttraction): Courtship needs the minimum
    /// romance opinion plus some romantic drive, or an earlier good date with them; Hookup needs the minimum hookup opinion
    /// and a pawn willing to hook up. Cheating and the recipient's answer are left to the callers.
    /// </summary>
    public static RomanticIntent GetRomanticIntent(Pawn initiator, Pawn recipient)
    {
      return GetRomanticIntent(initiator, recipient, out _);
    }

    // As above, with the deciding reason (English, for the dev romance menu and debug log).
    public static RomanticIntent GetRomanticIntent(Pawn initiator, Pawn recipient, out string reason)
    {
      if (initiator == recipient || !PsycheHelper.PsychologyEnabled(initiator) || !PsycheHelper.PsychologyEnabled(recipient))
      {
        reason = "no psyche";
        return RomanticIntent.None;
      }
      if (!SpeciesHelper.RomanceEnabled(initiator, true) || !SpeciesHelper.RomanceEnabled(recipient, true))
      {
        reason = "too young to date (or species can't)";
        return RomanticIntent.None;
      }
      if (LovePartnerRelationUtility.LovePartnerRelationExists(initiator, recipient))
      {
        reason = "partners";
        return RomanticIntent.DateNight;
      }
      float attraction = initiator.relations.SecondaryRomanceChanceFactor(recipient);
      if (attraction < MinAttraction)
      {
        reason = $"not attracted ({attraction:0.00} < {MinAttraction:0.00})";
        return RomanticIntent.None;
      }
      float opinion = initiator.relations.OpinionOf(recipient);
      float romanticDrive = PsycheHelper.Comp(initiator).Sexuality.AdjustedRomanticDrive;
      int goodDates = GoodDateCount(initiator, recipient);
      if (opinion >= PsychologySettings.romanceOpinionThreshold && (romanticDrive > 0f || goodDates > 0))
      {
        reason = $"attraction {attraction:0.00}, opinion {opinion:0}, romantic drive {romanticDrive:0.00}, good dates {goodDates}";
        return RomanticIntent.Courtship;
      }
      if (opinion >= PsychologySettings.minOpinionForHookup && WillPawnTryHookup(initiator))
      {
        reason = $"attraction {attraction:0.00}, opinion {opinion:0} (below romance minimum {PsychologySettings.romanceOpinionThreshold:0} or no romantic drive)";
        return RomanticIntent.Hookup;
      }
      reason = $"opinion {opinion:0} below romance minimum {PsychologySettings.romanceOpinionThreshold:0}, and no hookup (minimum {PsychologySettings.minOpinionForHookup:0}, or unwilling)";
      return RomanticIntent.None;
    }

    // Personality compatibility in [-1, 1] (0 = a typical pair); see Pawn_PsycheTracker.PersonalityCompatibilityWith.
    public static float Compatibility(Pawn pawn, Pawn other)
    {
      return PsycheHelper.PsychologyEnabled(pawn) ? PsycheHelper.Comp(pawn).Psyche.PersonalityCompatibilityWith(other) : 0f;
    }

    // How many current memories of this def pawn has about other.
    public static int CountMemoriesOf(Pawn pawn, Pawn other, ThoughtDef def)
    {
      List<Thought_Memory> memories = pawn?.needs?.mood?.thoughts?.memories?.Memories;
      if (memories == null)
      {
        return 0;
      }
      int count = 0;
      for (int i = 0; i < memories.Count; i++)
      {
        if (memories[i].def == def && memories[i].otherPawn == other)
        {
          count++;
        }
      }
      return count;
    }

    // Good courtship dates pawn remembers having with other (DateWentWell memories last 20 days, up to 5 per pawn).
    public static int GoodDateCount(Pawn pawn, Pawn other)
    {
      return CountMemoriesOf(pawn, other, ThoughtDefOfPsychology.DateWentWell);
    }

    /// <summary>
    /// How a date went for <paramref name="pawn"/>, from -1 (awful) to 1 (wonderful): 0.5 x personality compatibility,
    /// 0.3 x mood (-1 at 0%, +1 at 100%), 0.2 x opinion of the other / 100, luck (+/-0.3), and a 0.1 lean so a typical
    /// date between two content pawns who like each other is mildly good.
    /// </summary>
    public static float DateQuality(Pawn pawn, Pawn other)
    {
      float mood = pawn.needs?.mood?.CurLevel ?? 0.5f;
      float quality = 0.1f + 0.5f * Compatibility(pawn, other) + 0.3f * (2f * mood - 1f) + 0.2f * pawn.relations.OpinionOf(other) / 100f + Rand.Range(-0.3f, 0.3f);
      return Mathf.Clamp(quality, -1f, 1f);
    }

    /// <summary>
    /// Gives <paramref name="pawn"/> a "good date" or "bad date" memory of <paramref name="other"/> on a sliding scale:
    /// quality x the good date opinion setting, or |quality| x the bad date opinion setting. Dates too forgettable to move
    /// opinion by half a point leave no memory.
    /// </summary>
    public static void GainDateMemory(Pawn pawn, Pawn other, float quality)
    {
      if (pawn.needs?.mood?.thoughts?.memories == null)
      {
        return;
      }
      bool good = quality >= 0f;
      float offset = Mathf.Abs(quality) * (good ? PsychologySettings.dateOpinionGood : PsychologySettings.dateOpinionBad);
      if (Mathf.Abs(offset) < 0.5f)
      {
        return;
      }
      Thought_MemorySocial thought = (Thought_MemorySocial)ThoughtMaker.MakeThought(good ? ThoughtDefOfPsychology.DateWentWell : ThoughtDefOfPsychology.DateWentBadly);
      thought.opinionOffset = offset;
      pawn.needs.mood.thoughts.memories.TryGainMemory(thought, other);
    }

    /// <summary>
    /// Chance a courtship date that went well for both ends in a romance attempt: the base setting x a mood factor (x0.5
    /// to x1.5 by the lower of their moods, 20% to 90%) x an opinion factor (none below the minimum romance opinion, then
    /// x0.5 to x2 by the lower of their opinions, up to 100) x (1 + the per-date bonus x their earlier good dates).
    /// </summary>
    public static float RomanceChanceAfterDate(Pawn a, Pawn b, float previousGoodDates)
    {
      float minOpinion = Mathf.Min(a.relations.OpinionOf(b), b.relations.OpinionOf(a));
      if (minOpinion < PsychologySettings.romanceOpinionThreshold)
      {
        return 0f;
      }
      float minMood = Mathf.Min(a.needs?.mood?.CurLevel ?? 0.5f, b.needs?.mood?.CurLevel ?? 0.5f);
      float moodFactor = Mathf.Lerp(0.5f, 1.5f, Mathf.InverseLerp(0.2f, 0.9f, minMood));
      float opinionFactor = Mathf.Lerp(0.5f, 2f, Mathf.InverseLerp(PsychologySettings.romanceOpinionThreshold, 100f, minOpinion));
      float historyFactor = 1f + PsychologySettings.dateRomanceBonus / 100f * previousGoodDates;
      return Mathf.Clamp01(PsychologySettings.dateRomanceChance / 100f * moodFactor * opinionFactor * historyFactor);
    }

    /// <summary>
    /// Fires a vanilla romance attempt (so Psychology's success chance patches apply) from the bolder of the two
    /// (romantic drive + Confident) at the other. True if they end up partners. It isn't routed to a date or hookup
    /// (RomanceAttemptRouter); the attempt itself logs the debug line.
    /// </summary>
    public static bool TryFireRomanceAttempt(Pawn a, Pawn b)
    {
      Pawn initiator = Boldness(a) >= Boldness(b) ? a : b;
      Pawn recipient = initiator == a ? b : a;
      RomanceAttemptRouter.directReason = "after a good date";
      try
      {
        ForceInteraction(initiator, recipient, InteractionDefOf.RomanceAttempt);
      }
      finally
      {
        RomanceAttemptRouter.directReason = null;
      }
      return LovePartnerRelationUtility.LovePartnerRelationExists(initiator, recipient);
    }

    /// <summary>
    /// Runs an interaction's worker directly, with the usual log entry, bubble and letter. Used where TryInteractWith
    /// would refuse because the pawns aren't standing close enough at that moment (end of a date, the dev romance menu).
    /// </summary>
    public static void ForceInteraction(Pawn initiator, Pawn recipient, InteractionDef def)
    {
      List<RulePackDef> extraSentencePacks = new List<RulePackDef>();
      def.Worker.Interacted(initiator, recipient, extraSentencePacks, out string letterText, out string letterLabel, out LetterDef letterDef, out LookTargets lookTargets);
      PlayLogEntry_Interaction entry = new PlayLogEntry_Interaction(def, initiator, recipient, extraSentencePacks);
      Find.PlayLog.Add(entry);
      if (initiator.Spawned && recipient.Spawned)
      {
        MoteMaker.MakeInteractionBubble(initiator, recipient, def.interactionMote, def.GetSymbol());
      }
      if (letterDef != null)
      {
        string text = entry.ToGameStringFromPOV(initiator);
        if (!letterText.NullOrEmpty())
        {
          text = text + "\n\n" + letterText;
        }
        Find.LetterStack.ReceiveLetter(letterLabel, text, letterDef, lookTargets ?? new LookTargets(initiator));
      }
    }

    private static float Boldness(Pawn pawn)
    {
      if (!PsycheHelper.PsychologyEnabled(pawn))
      {
        return 0f;
      }
      return PsycheHelper.Comp(pawn).Sexuality.AdjustedRomanticDrive + PsycheHelper.Comp(pawn).Psyche.GetPersonalityRating(PersonalityNodeDefOf.Confident);
    }

    /// <summary>
    /// Determines if <paramref name="target"/> agrees to a hookup with <paramref name="asker"/>. Takes cheating into account.
    /// </summary>
    /// <param name="target"></param>
    /// <param name="asker"></param>
    /// <returns>True or False</returns>
    // The target's decision, rolled against HookupAcceptChance.
    public static bool IsHookupAppealing(Pawn target, Pawn asker)
    {
      float chance = HookupAcceptChance(target, asker, out string details);
      bool accepted = Rand.Chance(chance);
      PsychologyDebug.Log($"{target} considers a hookup with {asker}: chance {chance:P0} ({details}) -> {(accepted ? "yes" : "no")}");
      return accepted;
    }

    // Chance (0-1) the target agrees to a hookup: needs the minimum hookup opinion and not asexual (unless already
    // partners); then the cheat chance if it would be cheating x InverseLerp(0.05, 1, attraction (reduced 1.5x for
    // non-partners) x an opinion factor). details explains it (English, for the debug log).
    public static float HookupAcceptChance(Pawn target, Pawn asker, out string details)
    {
      if (!PsycheHelper.PsychologyEnabled(target) || !PsycheHelper.PsychologyEnabled(asker))
      {
        details = "no psyche";
        return 0f;
      }
      float opinion = target.relations.OpinionOf(asker);
      if (opinion < PsychologySettings.minOpinionForHookup)
      {
        details = $"opinion {opinion:0} below hookup minimum {PsychologySettings.minOpinionForHookup:0}";
        return 0f;
      }
      bool partners = LovePartnerRelationUtility.LovePartnerRelationExists(target, asker);
      bool asexual = PsychologySettings.enableKinsey ? PsycheHelper.Comp(target).Sexuality.IsAsexual : target.story.traits.HasTrait(TraitDefOf.Asexual);
      //Otherwise their rating is already factored in via secondary romance chance factor
      if (asexual && !partners)
      {
        details = "asexual";
        return 0f;
      }
      float cheat = CheatFactor(target, asker, out Pawn cheatOn);
      float romanceFactor = target.relations.SecondaryRomanceChanceFactor(asker);
      if (!partners)
      {
        romanceFactor /= 1.5f;
      }
      //Decrease if opinion is negative, increase if positive but on a lesser scale
      float opinionFactor = Mathf.InverseLerp(-100f, 0f, opinion) * GenMath.LerpDouble(0, 100f, 1f, 1.5f, opinion);
      float appeal = Mathf.InverseLerp(0.05f, 1f, romanceFactor * opinionFactor);
      details = $"attraction {romanceFactor:0.00} x opinion {opinionFactor:0.00} -> appeal {appeal:0.00} x cheat {cheat:0.00}{(cheatOn != null ? $" (on {cheatOn})" : "")}";
      return Mathf.Clamp01(cheat * appeal);
    }

    /// <summary>
    /// Determines if <paramref name="target"/> accepts a date with <paramref name="asker"/>.
    /// </summary>
    /// <param name="target"></param>
    /// <param name="asker"></param>
    /// <returns>True or false</returns>

    /// <summary>
    /// Will <paramref name="pawn"/> participate in a hookup. Checks settings and asexuality rating.
    /// </summary>
    /// <param name="pawn">The pawn in question</param>
    /// <returns>True or False</returns>
    // Whether a pawn would initiate a hookup at all: psyche, hookups enabled, old enough for lovin, not asexual, and
    // their ideoligion allows sharing a bed.
    public static bool WillPawnTryHookup(Pawn pawn)
    {
      if (!PsycheHelper.PsychologyEnabled(pawn))
      {
        return false;
      }
      if (PsychologySettings.hookupRateMultiplier == 0f)
      {
        return false;
      }
      SpeciesSettings settings = SpeciesHelper.GetOrMakeSpeciesSettingsFromThingDef(pawn.def);
      if (settings == null)
      {
        return false;
      }
      if (!SpeciesHelper.RomanceEnabled(pawn, false))
      {
        return false;
      }
      // ToDo: perhaps add a species specific enable hookup option

      if (PsychologySettings.enableKinsey)
      {
        if (PsycheHelper.Comp(pawn).Sexuality.IsAsexual)
        {
          return false;
        }
      }
      else if (pawn.story.traits.HasTrait(TraitDefOf.Asexual))
      {
        return false;
      }
      //If their ideo prohibits all lovin', do not allow
      if (!new HistoryEvent(HistoryEventDefOf.SharedBed, pawn.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo())
      {
        return false;
      }
      return true;
    }

    /// <summary>
    /// Returns false if <paramref name="pawn"/> is close to needing to eat/sleep, there's enemies nearby, they're drafted, or they're doing a job that should not be interrupted
    /// </summary>
    /// <param name="pawn"></param>
    /// <returns>True or False</returns>
    // Not busy: no urgent needs, no enemies nearby, not drafted or in labour, and not on a forced/uninterruptible or
    // "serious" job (jobDefsThatPreventHookups).
    public static bool IsPawnFree(Pawn pawn)
    {
      if (!PsycheHelper.PsychologyEnabled(pawn))
      {
        return false;
      }
      if (PawnUtility.WillSoonHaveBasicNeed(pawn) || PawnUtility.EnemiesAreNearby(pawn) || pawn.Drafted)
      {
        return false;
      }
      if (pawn?.health?.hediffSet?.HasHediff(HediffDefOf.PregnancyLabor) != false || pawn?.health?.hediffSet?.HasHediff(HediffDefOf.PregnancyLaborPushing) != false)
      {
        return false;
      }
      if (pawn?.CurJob is Job job)
      {
        if (job?.playerForced == true)
        {
          return false;
        }
        if (pawn?.jobs?.IsCurrentJobPlayerInterruptible() == false)
        {
          return false;
        }
        if (job?.def is JobDef def && jobDefsThatPreventHookups.Contains(def))
        {
          return false;
        }
      }
      return true;

    }

    /// <summary>
    /// Grabs the first non-spouse love partner of the opposite gender. For use in generating parents.
    /// </summary>
    /// <param name="pawn"></param>
    /// <returns></returns>
    public static Pawn GetFirstLoverOfOppositeGender(Pawn pawn)
    {
      foreach (Pawn lover in GetNonSpouseLovers(pawn, true))
      {
        if (pawn.gender.Opposite() == lover.gender)
        {
          return lover;
        }
      }
      return null;
    }

    /// <summary>
    /// Generates a list of love partners that does not include spouses or fiances
    /// </summary>
    /// <param name="pawn"></param>
    /// <param name="includeDead"></param>
    /// <returns></returns>
    public static List<Pawn> GetNonSpouseLovers(Pawn pawn, bool includeDead)
    {
      List<Pawn> list = new List<Pawn>();
      if (!pawn.RaceProps.IsFlesh)
      {
        return list;
      }
      List<DirectPawnRelation> relations = pawn.relations.DirectRelations;

      foreach (DirectPawnRelation rel in relations)
      {
        if ((rel.def == PawnRelationDefOf.Lover || rel.def == PawnRelationDefOf.Fiance) && (includeDead || !rel.otherPawn.Dead))
        {
          list.Add(rel.otherPawn);
        }
      }
      return list;
    }

    /// <summary>
    /// Generates a list of pawns that are in love relations with <paramref name="pawn"/>. Pawns are only listed once, even if they are in more than one love relation.
    /// </summary>
    /// <param name="pawn"></param>
    /// <param name="includeDead">Whether dead pawns are added to the list</param>
    /// <param name="onMap">Whether pawns must be on the same map to be added to the list</param>
    /// <returns>A list of pawns that in a love relation with <paramref name="pawn"/></returns>
    public static List<Pawn> GetAllLoveRelationPawns(Pawn pawn, bool includeDead, bool onMap)
    {
      List<Pawn> list = new List<Pawn>();
      if (!pawn.RaceProps.IsFlesh)
      {
        return list;
      }
      foreach (DirectPawnRelation rel in LovePartnerRelationUtility.ExistingLovePartners(pawn, includeDead))
      {
        if (!list.Contains(rel.otherPawn))
        {
          if (pawn.Map == rel.otherPawn.Map)
          {
            list.Add(rel.otherPawn);
          }
          else if (!onMap)
          {
            list.Add(rel.otherPawn);
          }
        }
      }
      return list;
    }

    /// <summary>
    /// Finds the most liked pawn with a specific <paramref name="relation"/> to <paramref name="pawn"/>. Any direct relation will work, no implied relations.
    /// </summary>
    /// <param name="pawn"></param>
    /// <param name="relation"></param>
    /// <param name="allowDead"></param>
    /// <returns></returns>
    public static Pawn GetMostLikedOfRel(Pawn pawn, PawnRelationDef relation, bool allowDead)
    {
      List<DirectPawnRelation> list = pawn.relations.DirectRelations;
      Pawn result = null;
      int num = 0;
      foreach (DirectPawnRelation rel in list)
      {
        if (rel.def == relation && (rel.otherPawn.Dead || allowDead))
        {
          if (pawn.relations.OpinionOf(rel.otherPawn) > num)
          {
            num = pawn.relations.OpinionOf(rel.otherPawn);
            result = rel.otherPawn;
          }
        }
      }
      return result;
    }

    public static bool HookupAllowed(this Pawn pawn)
    {
      if (!PsycheHelper.PsychologyEnabled(pawn))
      {
        return false;
      }
      if (!SpeciesHelper.RomanceEnabled(pawn, false))
      {
        return false;
      }
      return true;
    }

    /// <summary>
    /// A rating to use for determining sex aversion for asexual pawns. Seed is based on pawn's ID, so it will always return the same number for a given pawn.
    /// </summary>
    /// <param name="pawn"></param>
    /// <returns>float between 0 and 1</returns>

    /// <summary>
    /// Generates points for the lovin age curve based on age settings
    /// </summary>
    /// <returns>List<CurvePoint></returns>
  }
}