using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace Psychology
{
    // Remembers which table on this map is the mayor's office (not saved: rebuilt from the comps when they spawn).
    class OfficeTableMapComponent : MapComponent
    {
        public CompPotentialOfficeTable officeTable;

        public OfficeTableMapComponent(Map map) : base(map)
        {
            officeTable = null;
        }
    }
}
