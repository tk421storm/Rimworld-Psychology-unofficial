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
    // Mayor meeting toil: counts the ticks both pawns spend in the gathering area (drives the memories in
    // LordJob_VisitMayor) and occasionally trains the mayor's Social skill.
    public class LordToil_Meeting : LordToil
    {
        public LordToil_Meeting(IntVec3 spot)
        {
            this.spot = spot;
        }

        public override void UpdateAllDuties()
        {
            for (int i = 0; i < this.lord.ownedPawns.Count; i++)
            {
                this.lord.ownedPawns[i].mindState.duty = new PawnDuty(DutyDefOfPsychology.Meeting, this.spot, -1f);
            }
        }

        public override void LordToilTick()
        {
            base.LordToilTick();
            LordJob_VisitMayor meeting = this.lord.LordJob as LordJob_VisitMayor;
            for (int i = 0; i < this.lord.ownedPawns.Count; i++)
            {
                // IntVec3 is a struct, so the old "== null" checks were always false; the 1.6 build uses IsValid instead.
                if (!this.lord.ownedPawns[i].Position.IsValid || !this.spot.IsValid || this.lord.ownedPawns[i].Map == null || !GatheringsUtility.InGatheringArea(this.lord.ownedPawns[i].Position, this.spot, this.lord.ownedPawns[i].Map))
                {
                    return;
                }
            }
            if (meeting != null)
            {
                meeting.ticksInSameRoom += 1;
                if (meeting.ticksInSameRoom % 500 == 0 && Rand.Value < 0.5f)
                {
                    meeting.mayor.skills.Learn(SkillDefOf.Social, 0.5f);
                    MoteMaker.MakeInteractionBubble(meeting.constituent, meeting.mayor, InteractionDefOf.DeepTalk.interactionMote, InteractionDefOf.DeepTalk.GetSymbol()); // TEST in 1.3
                }
            }
        }

        private IntVec3 spot;
    }
}
