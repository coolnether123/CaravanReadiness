using System;

namespace CaravanReadiness.Domain
{
    /// <summary>
    /// Reconciles mutually exclusive cargo quantities so the observational UI
    /// cannot count one stack in more than one readiness category.
    /// </summary>
    public readonly struct CargoCountLedger
    {
        public CargoCountLedger(
            int requested,
            int loaded,
            int carried,
            int reserved,
            int waiting,
            int unavailable,
            int inaccessible,
            int blocked)
        {
            Requested = requested;
            Loaded = loaded;
            Carried = carried;
            Reserved = reserved;
            Waiting = waiting;
            Unavailable = unavailable;
            Inaccessible = inaccessible;
            Blocked = blocked;
        }

        public int Requested { get; }
        public int Loaded { get; }
        public int Carried { get; }
        public int Reserved { get; }
        public int Waiting { get; }
        public int Unavailable { get; }
        public int Inaccessible { get; }
        public int Blocked { get; }
        public int Remaining => Requested - Loaded;

        public static CargoCountLedger Create(
            int initialRequested,
            int remaining,
            int compatibleInventory,
            int carried,
            int reserved,
            int accessible,
            int inaccessible,
            int blocked)
        {
            initialRequested = NonNegative(initialRequested);
            remaining = Math.Min(NonNegative(remaining), initialRequested);
            int expectedLoaded = initialRequested - remaining;
            int loaded = Math.Min(NonNegative(compatibleInventory), expectedLoaded);

            // Dropping a loaded item or reducing the vanilla manifest lowers
            // the effective request without inventing phantom progress.
            int requested = remaining + loaded;
            int open = remaining;
            int carriedCount = Take(ref open, carried);
            int reservedCount = Take(ref open, reserved);
            int blockedCount = Take(ref open, blocked);
            int inaccessibleCount = Take(ref open, inaccessible);
            int waitingCount = Take(ref open, accessible);

            return new CargoCountLedger(
                requested,
                loaded,
                carriedCount,
                reservedCount,
                waitingCount,
                open,
                inaccessibleCount,
                blockedCount);
        }

        private static int Take(ref int available, int requested)
        {
            int result = Math.Min(available, NonNegative(requested));
            available -= result;
            return result;
        }

        private static int NonNegative(int value)
        {
            return value < 0 ? 0 : value;
        }
    }
}
