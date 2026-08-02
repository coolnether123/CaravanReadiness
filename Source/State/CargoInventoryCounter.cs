using System.Collections.Generic;
using CaravanReadiness.Domain;
using RimWorld;
using Verse;

namespace CaravanReadiness.State
{
    internal static class CargoInventoryCounter
    {
        public static int Count(
            TransferableOneWay transferable,
            IEnumerable<Pawn> carriers)
        {
            Thing representative = transferable?.AnyThing;
            if (representative == null || carriers == null)
            {
                return 0;
            }

            int count = 0;
            foreach (Pawn carrier in carriers)
            {
                if (carrier?.inventory == null)
                {
                    continue;
                }

                // UnloadEverything is a delayed vanilla cancellation flag,
                // not an ownership test. It can remain set while the next
                // formation has already placed selected cargo into this
                // pawn's inventory. The transferable group remains the
                // authoritative membership check below, so excluding the
                // whole inventory here loses real loaded cargo.

                foreach (Thing item in carrier.inventory.innerContainer)
                {
                    if (TransferableUtility.TransferAsOne(
                        representative,
                        item,
                        TransferAsOneMode.PodsOrCaravanPacking))
                    {
                        count += item.stackCount;
                    }
                }
            }
            return count;
        }
    }
}
