using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace Psychology;

// ToDo: fix this with patch or class extension
// Meant for the DrugFree hediff (result of treating chemical interest/fascination): end any drug binge immediately.
// BUG (pre-existing): not wired up. The DrugFree HediffDef uses HediffWithComps (TreatmentBase), so this class is never
// instantiated; new binges are still blocked by MentalStateWorker_BingingDrugPatch, but one already running isn't ended.
// Calling PostEnd() directly also wouldn't clear the mental state; RecoverFromState() would.
public class Hediff_DrugFree : Hediff
{
    public override void PostTick()
    {
        base.PostTick();
        if (pawn.InMentalState && (pawn.MentalState.def == DefDatabase<MentalStateDef>.GetNamed("BingingDrugMajor") || (pawn.MentalState.def == DefDatabase<MentalStateDef>.GetNamed("BingingDrugExtreme"))))
        {
            pawn.MentalState.PostEnd();
        }
    }
}
