using System;

namespace CaravanReadiness.Domain
{
    /// <summary>
    /// Bounds per-stack classification so overlapping reservations and hazards
    /// cannot account for more units than the stack actually contains.
    /// </summary>
    public static class StackQuantityAllocator
    {
        public static int Allocate(
            int stackCount,
            int alreadyClassified,
            int requested)
        {
            int capacity = Math.Max(0, stackCount);
            int used = Math.Max(0, Math.Min(capacity, alreadyClassified));
            int wanted = Math.Max(0, requested);
            return Math.Min(capacity - used, wanted);
        }

        public static int Remaining(int stackCount, int classified)
        {
            return Math.Max(
                0,
                Math.Max(0, stackCount) - Math.Max(0, classified));
        }
    }
}
