using CaravanReadiness.Patches;
using Spine.Api;
using Verse;

namespace CaravanReadiness.Bootstrap
{
    /// <summary>
    /// Acquires the shared runtime capabilities needed by this settings-free
    /// mod and keeps startup wiring out of caravan state and presentation.
    /// </summary>
    public sealed class CaravanReadinessMod : Mod
    {
        private static System.IDisposable tooltipLease;

        public CaravanReadinessMod(ModContentPack content)
            : base(content)
        {
            SpineApi.Runtime.Require(new SpineRequirement(
                "CoolNether123.CaravanReadiness",
                new SemanticVersion(1, 0, 0),
                SpineCapability.HarmonyPatching |
                SpineCapability.TooltipSizing));

            tooltipLease ??= SpineApi.Tooltips.Acquire(
                "CoolNether123.CaravanReadiness");
            FormationLifecyclePatches.Install();
        }
    }
}
