using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Reflection;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;
using Verse.Grammar;
using UnityEngine;

namespace Psychology;

/// <summary>
/// Per-game state and periodic logic:
///   - A cache of each pawn's personality-driven daily certainty change (Ideology).
///   - On load, syncs vanilla orientation traits with the Kinsey setting.
/// </summary>
public class PsychologyGameComponent : GameComponent
{
  public bool firstTimeWithUpdate = true;
  public bool taraiSiblingsGenerated = false;
  public Dictionary<int, float> CachedCertaintyChangePerDayDict = new Dictionary<int, float>();
  // Static (not saved).
  public static int certaintyTick = 12;

  public PsychologyGameComponent(Game game)
  {
  }

  public override void ExposeData()
  {
    Scribe_Values.Look(ref this.firstTimeWithUpdate, "Psychology_FirstTimeWithUpdate", true);
    Scribe_Values.Look(ref taraiSiblingsGenerated, "Psychology_TaraiSiblingsGenerated", false);
  }

  // ToDo: check which of these things causes crash
  public override void LoadedGame()
  {
    //Log.Message("Psychology: loading game");
    //SpeciesHelper.RegisterHumanlikeSpeciesLoadedGame(); //ToDo: figure out why this causes crash
    ImplementSexualOrientation();
  }

  public override void GameComponentTick()
  {
    if (GenTicks.TicksGame % 250 == 97)
    {
      certaintyTick++;
    }
  }

  // Cached CalculateCertaintyChangePerDay. Each pawn's entry is refreshed whenever (id + certaintyTick) % 100 == 37, i.e.
  // roughly once every 100 * 250 ticks (~10 in-game hours), spread out across pawns.
  public float CertaintyChange(Pawn pawn, bool addToDicts)
  {
    float certaintyChange;
    int idnumber;
    idnumber = pawn.thingIDNumber;
    if (!CachedCertaintyChangePerDayDict.TryGetValue(idnumber, out certaintyChange) || (idnumber + certaintyTick) % 100 == 37)
    {
      certaintyChange = PsycheHelper.Comp(pawn).Psyche.CalculateCertaintyChangePerDay(pawn.Ideo, addToDicts);
      CachedCertaintyChangePerDayDict[idnumber] = certaintyChange;
    }
    return certaintyChange;
  }

  public virtual void ImplementSexualOrientation()
  {
    if (PsychologySettings.enableKinsey)
    {
      foreach (Pawn pawn in PawnsFinder.All_AliveOrDead)
      {
        PsycheHelper.CorrectTraitsForPawnKinseyEnabled(pawn);
      }
    }
    else
    {
      foreach (Pawn pawn in PawnsFinder.All_AliveOrDead)
      {
        PsycheHelper.CorrectTraitsForPawnKinseyDisabled(pawn);
      }
    }
  }
}
