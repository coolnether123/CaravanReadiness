using System;
using System.Runtime.CompilerServices;
using CaravanReadiness.Domain;

namespace CaravanReadiness.Tests
{
    internal static class ManifestRequestTrackerTests
    {
        [ModuleInitializer]
        internal static void Run()
        {
            Equal(40, ManifestRequestTracker.Observe(40, 40, 0, 33, 7),
                "loading preserves request");
            Equal(43, ManifestRequestTracker.Observe(40, 33, 7, 36, 7),
                "manifest addition raises request");
            Equal(16, ManifestRequestTracker.Observe(20, 20, 0, 16, 0),
                "manifest removal lowers request");
            Equal(37, ManifestRequestTracker.Observe(40, 33, 7, 33, 4),
                "dropped cargo lowers request");
            Equal(0, ManifestRequestTracker.Observe(2, 2, 0, 0, 0),
                "request never becomes negative");
            Console.WriteLine("PASS: 5 manifest request observations");
        }

        private static void Equal(int expected, int actual, string scenario)
        {
            if (expected != actual)
            {
                throw new InvalidOperationException(
                    scenario + ": expected " + expected + ", actual " + actual);
            }
        }
    }
}
