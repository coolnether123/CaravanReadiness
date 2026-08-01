using System;
using System.Collections.Generic;

namespace CaravanReadiness.Domain
{
    public static class ManifestSlotReconciler
    {
        public const int NewSlot = -1;

        public static int[] Match(
            IReadOnlyList<string> previousDefinitions,
            IReadOnlyList<string> currentDefinitions)
        {
            int previousCount = previousDefinitions?.Count ?? 0;
            int currentCount = currentDefinitions?.Count ?? 0;
            bool[] used = new bool[previousCount];
            int[] matches = new int[currentCount];

            for (int currentIndex = 0;
                 currentIndex < currentCount;
                 currentIndex++)
            {
                string definition = currentDefinitions[currentIndex];
                int match = FindExact(
                    previousDefinitions,
                    used,
                    definition);
                if (match < 0 && definition == null &&
                    currentIndex < previousCount &&
                    !used[currentIndex])
                {
                    match = currentIndex;
                }

                matches[currentIndex] = match;
                if (match >= 0)
                {
                    used[match] = true;
                }
            }

            return matches;
        }

        private static int FindExact(
            IReadOnlyList<string> previousDefinitions,
            bool[] used,
            string definition)
        {
            if (definition == null)
            {
                return NewSlot;
            }

            for (int index = 0; index < used.Length; index++)
            {
                if (!used[index] && string.Equals(
                    previousDefinitions[index],
                    definition,
                    StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return NewSlot;
        }
    }
}
