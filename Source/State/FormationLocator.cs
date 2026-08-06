using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using CaravanReadiness.Domain;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace CaravanReadiness.State
{
    /// <summary>
    /// Resolves player caravan lords for a packing spot through vanilla's
    /// private meeting point while returning a deterministic formation order.
    /// </summary>
    internal static class FormationLocator
    {
        private static readonly AccessTools.FieldRef<
            LordJob_FormAndSendCaravan,
            IntVec3> MeetingPointRef =
                AccessTools.FieldRefAccess<LordJob_FormAndSendCaravan, IntVec3>(
                    "meetingPoint");

        public static IntVec3 MeetingPoint(LordJob_FormAndSendCaravan job)
        {
            return MeetingPointRef(job);
        }

        public static List<Lord> At(Map map, IntVec3 cell)
        {
            if (map?.lordManager?.lords == null)
            {
                return new List<Lord>();
            }

            List<Lord> formations = map.lordManager.lords
                .Where(lord =>
                    lord.faction == Faction.OfPlayer &&
                    lord.LordJob is LordJob_FormAndSendCaravan job &&
                    MeetingPoint(job) == cell)
                .ToList();
            formations.Sort((left, right) =>
                FormationOrdering.CompareLoadIds(
                    left.loadID,
                    right.loadID));
            return formations;
        }
    }
}
