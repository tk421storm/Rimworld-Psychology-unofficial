using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace Psychology
{
    // A room containing the active office table is an Office (huge score so it wins over other roles).
    class RoomRoleWorker_Office : RoomRoleWorker
    {

        public override float GetScore(Room room)
        {
            List<Thing> containedAndAdjacentThings = room.ContainedAndAdjacentThings;
            for (int i = 0; i < containedAndAdjacentThings.Count; i++)
            {
                Thing thing = containedAndAdjacentThings[i];
                Building building_Table = thing as Building;
                if (building_Table != null && building_Table.def.HasComp(typeof(CompPotentialOfficeTable)) && building_Table.GetComp<CompPotentialOfficeTable>().Active)
                {
                    return 100005f;
                }
            }
            return 0f;
        }
    }
}
