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
    // Shared toil for dates and hang-outs; also holds the pair's current joy job (see JobGiver_SpendTimeTogether).
    public class LordToil_HangOut : LordToil
    {
        public LordToil_HangOut(Pawn[] pawns)
        {
            this.friends = pawns;
        }

        // Keeps the pair talking to each other, not to whoever happens to be nearby: vanilla's most sociable rate (an
        // interaction every 550 ticks on average, checked every 60), picking the interaction by the usual weights, as long
        // as both can talk and are close enough.
        public override void LordToilTick()
        {
            if (this.friends == null || this.friends.Length < 2 || !this.friends[0].IsHashIntervalTick(60) || !Rand.MTBEventOccurs(550f, 1f, 60f))
            {
                return;
            }
            Pawn initiator = Rand.Bool ? this.friends[0] : this.friends[1];
            Pawn recipient = initiator == this.friends[0] ? this.friends[1] : this.friends[0];
            if (!initiator.Spawned || !recipient.Spawned || initiator.interactions == null || initiator.interactions.InteractedTooRecentlyToInteract())
            {
                return;
            }
            if (!SocialInteractionUtility.CanInitiateRandomInteraction(initiator) || !SocialInteractionUtility.CanReceiveRandomInteraction(recipient))
            {
                return;
            }
            if (DefDatabase<InteractionDef>.AllDefsListForReading.TryRandomElementByWeight(x => initiator.interactions.CanInteractNowWith(recipient, x) ? x.Worker.RandomSelectionWeight(initiator, recipient) : 0f, out InteractionDef interaction))
            {
                PsychologyDebug.Log($"Outing talk: {initiator} -> {recipient}: {interaction.defName}");
                initiator.interactions.TryInteractWith(recipient, interaction);
            }
        }

        public override void UpdateAllDuties()
        {
            for (int i = 0; i < this.lord.ownedPawns.Count; i++)
            {
                this.lord.ownedPawns[i].mindState.duty = new PawnDuty(DutyDefOfPsychology.HangOut, this.friends[0].Position, this.friends[1].Position, -1f);
            }
        }

        public Pawn[] friends;
        public Job hangOut;
        public int ticksToNextJoy = 0;
        public int tickSinceLastJobGiven = 0;
    }
}
