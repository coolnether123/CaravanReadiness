using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;
using CaravanReadiness.State;
using Spine.Api;
using Spine.Harmony;

namespace CaravanReadiness.Patches
{
    internal static class FormationLifecyclePatches
    {
        private const string HarmonyId = "CoolNether123.CaravanReadiness";
        private static readonly IHarmonyPatchInstaller Installer =
            SpineApi.Patching.CreateInstaller(
                HarmonyId,
                "[Caravan Readiness]");

        public static void Install()
        {
            Installer.TryPatch(
                "formation start",
                AccessTools.Method(
                    typeof(CaravanFormingUtility),
                    nameof(CaravanFormingUtility.StartFormingCaravan)),
                postfix: new HarmonyMethod(
                    typeof(FormationLifecyclePatches),
                    nameof(CaptureAfterStart)));
            Installer.TryPatch(
                "formation cleanup",
                AccessTools.Method(
                    typeof(LordManager),
                    nameof(LordManager.RemoveLord)),
                prefix: new HarmonyMethod(
                    typeof(FormationLifecyclePatches),
                    nameof(RemoveBeforeLordRemoval)));
        }

        private static void CaptureAfterStart(
            List<Pawn> pawns,
            List<TransferableOneWay> transferables,
            IntVec3 meetingPoint)
        {
            if (pawns == null || pawns.Count == 0 || pawns[0]?.Map == null)
            {
                return;
            }

            Map map = pawns[0].Map;
            Lord lord = map.lordManager.lords
                .FindLast(candidate =>
                    candidate.LordJob is LordJob_FormAndSendCaravan &&
                    candidate.ownedPawns.Contains(pawns[0]));
            if (!(lord?.LordJob is LordJob_FormAndSendCaravan job))
            {
                Log.Warning("[Caravan Readiness] Formation started but its vanilla lord could not be resolved.");
                return;
            }

            map.GetComponent<FormationBaselineComponent>().Capture(lord, job);
        }

        private static void RemoveBeforeLordRemoval(
            LordManager __instance,
            Lord oldLord)
        {
            if (!(oldLord?.LordJob is LordJob_FormAndSendCaravan))
            {
                return;
            }

            Map map = __instance?.map;
            if (map != null)
            {
                map.GetComponent<FormationBaselineComponent>().Remove(oldLord.loadID);
            }
        }
    }
}
