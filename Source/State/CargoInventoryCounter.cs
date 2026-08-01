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
                if (carrier?.inventory == null ||
                    carrier.inventory.UnloadEverything)
                {
                    continue;
                }

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
