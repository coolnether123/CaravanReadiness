using System;

namespace CaravanReadiness.Domain
{
    /// <summary>
    /// Infers the authoritative request total from successive vanilla
    /// remaining and inventory observations without replacing vanilla state.
    /// </summary>
    public static class ManifestRequestTracker
    {
        public static int Observe(
            int previousRequested,
            int previousRemaining,
            int previousInventory,
            int currentRemaining,
            int currentInventory)
        {
            long observed = Math.Max(0, previousRequested);
            observed += Math.Max(0, currentRemaining) -
                Math.Max(0, previousRemaining);
            observed += Math.Max(0, currentInventory) -
                Math.Max(0, previousInventory);
            return (int)Math.Max(0, Math.Min(int.MaxValue, observed));
        }
    }
}
