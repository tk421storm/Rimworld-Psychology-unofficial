using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Psychology
{
    // Marks a pawn as mayor of the settlement on worldTileElectedOn (title, "visit the mayor" meetings, re-election rules).
    // PsychologyGameComponent.Mayors is rebuilt from these hediffs when a game loads.
    public class Hediff_Mayor : Hediff
    {
        public int yearElected;
        public int worldTileElectedOn;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref this.yearElected, "yearElected", 0);
            Scribe_Values.Look(ref this.worldTileElectedOn, "worldTileElectedOn", 0);
        }

    }
}