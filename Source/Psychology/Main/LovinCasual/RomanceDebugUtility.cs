using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace Psychology;

/// <summary>
/// Dev-mode "DEV: Romance" button on colonists (CompPsychology.CompGetGizmosExtra) for testing dates and hookups. Lists
/// every other free colonist on the map with this pawn's RomanticIntent towards them and the chances involved, and fires
/// the matching action, skipping the random selection and cooldowns:
///   - DateNight: plans a date night (PlanDate interaction)
///   - Courtship: asks them out (AskOutOnDate interaction; the answer is still rolled)
///   - Hookup: starts the hookup job (the target still rolls whether to accept)
/// Also "start planned date now" and "finish this date now". Results go to the play log, and to the debug log when
/// "Print debug info" is on.
/// </summary>
[StaticConstructorOnStartup]
public static class RomanceDebugUtility
{
  private static readonly Texture2D Icon = ContentFinder<Texture2D>.Get("Things/Mote/Heart");

  public static Command_Action RomanceGizmo(Pawn pawn)
  {
    return new Command_Action
    {
      defaultLabel = "DEV: Romance",
      defaultDesc = "Force a date night, courtship date or hookup with another colonist, chosen by this pawn's romantic intent towards them, skipping the random chance and cooldowns. Also start a planned date or finish the current date now.",
      icon = Icon,
      action = () => Find.WindowStack.Add(new FloatMenu(Options(pawn)))
    };
  }

  private static List<FloatMenuOption> Options(Pawn pawn)
  {
    List<FloatMenuOption> options = new List<FloatMenuOption>();
    foreach (Hediff_PlannedDate planned in pawn.health.hediffSet.hediffs.OfType<Hediff_PlannedDate>())
    {
      options.Add(new FloatMenuOption($"Start planned {(planned.courtship ? "date" : "date night")} with {planned.partner?.LabelShort} now", () =>
      {
        planned.StartNow();
        PsychologyDebug.Log($"DEV: starting the planned date of {pawn} and {planned.partner} now");
      }));
    }
    if (pawn.GetLord() is Lord lord && lord.LordJob is LordJob_Date date)
    {
      options.Add(new FloatMenuOption($"Finish this {(date.courtship ? "date" : "date night")} now", () =>
      {
        PsychologyDebug.Log($"DEV: finishing the date of {date.initiator} and {date.recipient} now");
        date.Finished();
        lord.lordManager.RemoveLord(lord);
      }));
    }
    foreach (Pawn target in pawn.Map.mapPawns.FreeColonistsSpawned)
    {
      if (target != pawn)
      {
        options.Add(TargetOption(pawn, target));
      }
    }
    if (options.Count == 0)
    {
      options.Add(new FloatMenuOption("No other colonists on this map", null));
    }
    return options;
  }

  private static FloatMenuOption TargetOption(Pawn pawn, Pawn target)
  {
    RomanticIntent intent = RomanceUtility.GetRomanticIntent(pawn, target, out string reason);
    string label = $"{target.LabelShort}: {intent}";
    switch (intent)
    {
      case RomanticIntent.DateNight:
        {
          InteractionDef def = InteractionDefOfPsychology.PlanDate;
          string blocked = ((InteractionWorker_DateBase)def.Worker).BlockReason(pawn, target);
          label += $" (weight {def.Worker.RandomSelectionWeight(pawn, target):0.00}{(blocked != null ? ", normally blocked: " + blocked : "")})";
          return new FloatMenuOption(label, () =>
          {
            PsychologyDebug.Log($"DEV: {pawn} plans a date night with {target} ({reason})");
            RomanceUtility.ForceInteraction(pawn, target, def);
          });
        }
      case RomanticIntent.Courtship:
        {
          InteractionDef def = InteractionDefOfPsychology.AskOutOnDate;
          string blocked = ((InteractionWorker_DateBase)def.Worker).BlockReason(pawn, target);
          if (blocked == null && RomanceUtility.CountMemoriesOf(pawn, target, ThoughtDefOfPsychology.RebuffedMyDateRequest) > 0)
          {
            blocked = "they turned this pawn down recently";
          }
          float weight = InteractionWorker_AskOutOnDate.AskWeight(pawn, target, true, out string weightDetails);
          float accept = InteractionWorker_AskOutOnDate.AcceptChance(target, pawn);
          label += $" (ask weight {weight:0.00}, accept {accept:P0}{(blocked != null ? ", normally blocked: " + blocked : "")})";
          return new FloatMenuOption(label, () =>
          {
            PsychologyDebug.Log($"DEV: {pawn} asks {target} out ({reason}); ask weight {weight:0.00} = {weightDetails}");
            RomanceUtility.ForceInteraction(pawn, target, def);
          });
        }
      case RomanticIntent.Hookup:
        {
          float cheat = RomanceUtility.CheatFactor(pawn, target, out Pawn cheatOn);
          Building_Bed bed = JoyGiver_CasualHookup.FindHookupBed(pawn, target);
          label += $" (cheat chance {cheat:P0}{(cheatOn != null ? " on " + cheatOn.LabelShort : "")}{(bed == null ? ", no bed for two" : "")})";
          if (bed == null)
          {
            return new FloatMenuOption(label, null);
          }
          return new FloatMenuOption(label, () =>
          {
            PsychologyDebug.Log($"DEV: {pawn} goes to propose a hookup to {target} ({reason}); skipped cheat check {cheat:P0}");
            pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOfPsychology.LeadHookup, target, bed));
          });
        }
      default:
        return new FloatMenuOption($"{label} - {reason}", null);
    }
  }
}
