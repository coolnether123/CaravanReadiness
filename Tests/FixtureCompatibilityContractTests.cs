using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace CaravanReadiness.Tests
{
    internal static class FixtureCompatibilityContractTests
    {
        [ModuleInitializer]
        internal static void Run()
        {
            string repositoryRoot = ResolveRepositoryRoot();
            string project = File.ReadAllText(Path.Combine(
                repositoryRoot,
                "Developer",
                "CaravanReadiness.TestFixture",
                "Source",
                "CaravanReadiness.TestFixture.csproj"));
            string actions = File.ReadAllText(Path.Combine(
                repositoryRoot,
                "Developer",
                "CaravanReadiness.TestFixture",
                "Source",
                "CaravanReadinessDebugActions.cs"));
            string adapter = File.ReadAllText(Path.Combine(
                repositoryRoot,
                "Developer",
                "CaravanReadiness.TestFixture",
                "Source",
                "FixtureCaravanApi.cs"));
            string lifecycle = File.ReadAllText(Path.Combine(
                repositoryRoot,
                "Developer",
                "CaravanReadiness.TestFixture",
                "Source",
                "LegacyLifecycleComponent.cs"));
            string bridge = File.ReadAllText(Path.Combine(
                repositoryRoot,
                "Developer",
                "CaravanReadiness.TestFixture",
                "Source",
                "LegacyFixtureBridge.cs"));
            string benchmarkProject = File.ReadAllText(Path.Combine(
                repositoryRoot,
                "Developer",
                "CaravanReadiness.BenchmarkFixture",
                "Source",
                "CaravanReadiness.BenchmarkFixture.csproj"));
            string benchmarkSource = File.ReadAllText(Path.Combine(
                repositoryRoot,
                "Developer",
                "CaravanReadiness.BenchmarkFixture",
                "Source",
                "BenchmarkWorkloadComponent.cs"));

            Require(project.Contains("Reference Include=\"CaravanReadiness\""),
                "fixture references the shipping assembly");
            Require(!project.Contains("ProjectReference Include=\"..\\..\\..\\Source\\Mod.csproj\""),
                "fixture does not compile production source");
            Require(project.Contains("$(Configuration)\\Assemblies"),
                "fixture output is version-scoped");
            Require(adapter.Contains("TryFindRandomExitSpot"),
                "legacy exit-spot adapter is present");
            Require(adapter.Contains("TryForceDeparture"),
                "modern departure adapter is present");
            Require(adapter.Contains("TryStartFireIn"),
                "legacy fire adapter is present");
            Require(lifecycle.Contains("GameComponentTick"),
                "legacy automatic lifecycle bridge is present");
            Require(bridge.Contains("Legacy fixture ping"),
                "legacy public debug bridge is present");
            Require(!benchmarkProject.Contains("ProjectReference"),
                "benchmark fixture has no production project reference");
            Require(!benchmarkSource.Contains("using CaravanReadiness."),
                "benchmark workload has no production namespace reference");
            Require(!actions.Contains("TryFindClosestEdgeCellTo"),
                "actions do not bind directly to modern exit API");
            Require(!actions.Contains("ForceCaravanDepart"),
                "actions do not bind directly to modern departure API");
            Require(!actions.Contains("ReadinessSnapshotBuilder.Build"),
                "actions do not bind to production internals at compile time");
            Console.WriteLine("PASS: fixture compatibility contract");
        }

        private static string ResolveRepositoryRoot()
        {
            string currentRoot = Path.Combine(
                Environment.CurrentDirectory,
                "RimWorld",
                "Mods",
                "CaravanReadiness");
            if (File.Exists(Path.Combine(
                currentRoot,
                "Developer",
                "CaravanReadiness.TestFixture",
                "Source",
                "CaravanReadiness.TestFixture.csproj")))
            {
                return currentRoot;
            }

            string path = AppContext.BaseDirectory;
            for (int index = 0; index < 8; index++)
            {
                if (File.Exists(Path.Combine(
                    path,
                    "Developer",
                    "CaravanReadiness.TestFixture",
                    "Source",
                    "CaravanReadiness.TestFixture.csproj")))
                {
                    return path;
                }

                path = Directory.GetParent(path)?.FullName;
                if (path == null)
                {
                    break;
                }
            }

            string environmentRoot = Environment.GetEnvironmentVariable(
                "CARAVAN_READINESS_REPOSITORY");
            if (!string.IsNullOrEmpty(environmentRoot))
            {
                return environmentRoot;
            }

            throw new DirectoryNotFoundException(
                "CaravanReadiness repository root was not found");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
