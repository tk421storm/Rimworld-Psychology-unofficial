using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using UnityEngine;

namespace Psychology
{
    // Records who actually stood at the funeral (only pawns inside the gathering area count as attendees).
    public class LordToil_Funeral : LordToil
    {
        public LordToil_Funeral(IntVec3 spot)
        {
            this.spot = spot;
        }

        public override void UpdateAllDuties()
        {
            for (int i = 0; i < this.lord.ownedPawns.Count; i++)
            {
                this.lord.ownedPawns[i].mindState.duty = new PawnDuty(DutyDefOfPsychology.Funeral, this.spot, -1f);
            }
        }

        public override ThinkTreeDutyHook VoluntaryJoinDutyHookFor(Pawn p)
        {
            return DutyDefOfPsychology.Funeral.hook;
        }

        
        public override void LordToilTick()
        {
            base.LordToilTick();
            LordJob_Joinable_Funeral funeral = this.lord.LordJob as LordJob_Joinable_Funeral;
            for (int i = 0; i < this.lord.ownedPawns.Count; i++)
            {
                // IntVec3 is a struct, so the old "== null" checks were always false; the 1.6 build uses IsValid instead.
                if (!this.lord.ownedPawns[i].Position.IsValid || !this.spot.IsValid || this.lord.ownedPawns[i].Map == null || !GatheringsUtility.InGatheringArea(this.lord.ownedPawns[i].Position, this.spot, this.lord.ownedPawns[i].Map))
                {
                    return;
                }
                funeral.Attended(this.lord.ownedPawns[i]);
            }
        }

        public override bool AllowSatisfyLongNeeds
        {
            get
            {
                return true;
            }
        }

        private IntVec3 spot;
    }
}
