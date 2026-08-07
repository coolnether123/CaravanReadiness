using System;
using System.Collections.Generic;
using System.Linq;
using CaravanReadiness.Domain;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace CaravanReadiness.State
{
    /// <summary>
    /// Projects live formation, inventory, job, reservation, and reachability
    /// state into an immutable-per-refresh report without mutating vanilla.
    /// </summary>
    internal static class ReadinessSnapshotBuilder
    {
        public static FormationReadinessSnapshot Build(Lord lord)
        {
            LordJob_FormAndSendCaravan job =
                lord.LordJob as LordJob_FormAndSendCaravan;
            if (job == null)
            {
                return null;
            }

            Map map = lord.Map;
            FormationBaselineComponent baselines =
                map.GetComponent<FormationBaselineComponent>();
            baselines.ReconcileActiveLords();

            FormationReadinessSnapshot snapshot =
                new FormationReadinessSnapshot
                {
                    Lord = lord,
                    LordLoadId = lord.loadID,
                    DisplayName = "CR_FormationLabel".Translate(lord.loadID),
                    Phase = job.Status,
                    MeetingPoint = FormationLocator.MeetingPoint(job)
                };

            List<Pawn> carriers = lord.ownedPawns
                .Concat(job.downedPawns ?? Enumerable.Empty<Pawn>())
                .Where(pawn => pawn != null)
                .Distinct()
                .ToList();
            List<Pawn> haulers = map.mapPawns.FreeColonistsSpawned
                .Where(IsAvailableHauler)
                .ToList();
            baselines.ReconcileTransferSlots(lord, job.transferables);

            int rowCount = Math.Max(
                job.transferables.Count,
                baselines.RowCountFor(lord));
            for (int index = 0; index < rowCount; index++)
            {
                TransferableOneWay transferable = index < job.transferables.Count
                    ? job.transferables[index]
                    : null;
                if (transferable == null || !transferable.HasAnyThing)
                {
                    CargoReadinessRow unavailable = BuildUnavailableRow(
                        lord,
                        baselines,
                        index,
                        transferable);
                    if (unavailable != null)
                    {
                        AddCargoRow(snapshot, unavailable);
                    }
                    continue;
                }

                CargoReadinessRow row = BuildCargoRow(
                    lord,
                    transferable,
                    baselines,
                    index,
                    carriers,
                    haulers);
                AddCargoRow(snapshot, row);
            }

            snapshot.Cargo.Sort((left, right) =>
                string.Compare(left.Label, right.Label, StringComparison.CurrentCultureIgnoreCase));
            BuildMembers(snapshot, lord, job);
            AddCapacityProblem(snapshot, job);
            snapshot.Problems.Sort((left, right) =>
            {
                int severity = right.Severity.CompareTo(left.Severity);
                return severity != 0
                    ? severity
                    : string.Compare(
                        left.Label,
                        right.Label,
                        StringComparison.CurrentCultureIgnoreCase);
            });
            return snapshot;
        }

        private static CargoReadinessRow BuildUnavailableRow(
            Lord lord,
            FormationBaselineComponent baselines,
            int transferableIndex,
            TransferableOneWay transferable)
        {
            ThingDef definition = baselines.DefinitionFor(
                lord,
                transferableIndex);
            if (definition == null)
            {
                return null;
            }

            int remaining = transferable?.CountToTransfer ??
                baselines.RemainingFor(lord, transferableIndex);
            int requested = baselines.ObserveRequestedFor(
                lord,
                transferableIndex,
                remaining,
                0);
            return new CargoReadinessRow
            {
                Def = definition,
                Label = definition.LabelCap,
                Counts = CargoCountLedger.Create(
                    requested,
                    remaining,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0)
            };
        }

        private static void AddCargoRow(
            FormationReadinessSnapshot snapshot,
            CargoReadinessRow row)
        {
            snapshot.Cargo.Add(row);
            snapshot.RequestedTotal += row.Counts.Requested;
            snapshot.LoadedTotal += row.Counts.Loaded;
            snapshot.CarriedTotal += row.Counts.Carried;
            snapshot.ReservedTotal += row.Counts.Reserved;
            snapshot.MissingTotal += row.Counts.Remaining;
            AddCargoProblems(snapshot, row);
        }

        private static CargoReadinessRow BuildCargoRow(
            Lord lord,
            TransferableOneWay transferable,
            FormationBaselineComponent baselines,
            int transferableIndex,
            List<Pawn> carriers,
            List<Pawn> haulers)
        {
            Thing representative = transferable.AnyThing;
            int compatibleInventory = CargoInventoryCounter.Count(
                transferable,
                carriers);
            int requested = baselines.ObserveRequestedFor(
                lord,
                transferableIndex,
                transferable.CountToTransfer,
                compatibleInventory);

            int carried = 0;
            int reserved = 0;
            int blocked = 0;
            int accessible = 0;
            int inaccessible = 0;
            bool forbidden = false;
            bool burning = false;
            Thing navigationTarget = null;
            Dictionary<Thing, int> classifiedQuantities =
                new Dictionary<Thing, int>();

            // Classification follows vanilla progress: cargo already in hand
            // wins over reservations, and only the unclaimed stack remainder
            // is evaluated for reachability. This prevents double counting.
            foreach (Pawn pawn in lord.Map.mapPawns.AllPawnsSpawned)
            {
                if (pawn?.CurJob?.lord != lord ||
                    !(pawn.jobs?.curDriver is JobDriver_PrepareCaravan_GatherItems))
                {
                    continue;
                }

                Thing carriedThing = pawn.carryTracker?.CarriedThing;
                if (Matches(representative, carriedThing))
                {
                    carried += carriedThing.stackCount;
                    classifiedQuantities[carriedThing] =
                        carriedThing.stackCount;
                    navigationTarget ??= pawn;
                }
            }

            foreach (ReservationManager.Reservation reservationEntry in
                lord.Map.reservationManager.ReservationsReadOnly)
            {
                Thing target = reservationEntry.Target.Thing;
                if (!transferable.things.Contains(target) ||
                    !Matches(representative, target))
                {
                    continue;
                }

                int requestedCount = reservationEntry.StackCount < 0
                    ? target.stackCount
                    : reservationEntry.StackCount;
                classifiedQuantities.TryGetValue(
                    target,
                    out int alreadyClassified);
                int allocated = StackQuantityAllocator.Allocate(
                    target.stackCount,
                    alreadyClassified,
                    requestedCount);
                if (allocated <= 0)
                {
                    continue;
                }
                if (reservationEntry.Job?.lord == lord &&
                    reservationEntry.Job.def == JobDefOf.PrepareCaravan_GatherItems)
                {
                    reserved += allocated;
                }
                else
                {
                    blocked += allocated;
                    navigationTarget ??= target;
                }
                classifiedQuantities[target] =
                    alreadyClassified + allocated;
            }

            foreach (Thing candidate in transferable.things)
            {
                if (candidate == null)
                {
                    continue;
                }
                if (candidate.Destroyed ||
                    candidate.MapHeld != lord.Map ||
                    !candidate.SpawnedOrAnyParentSpawned)
                {
                    continue;
                }
                if (carriers.Any(pawn => pawn.inventory?.innerContainer.Contains(candidate) == true))
                {
                    continue;
                }

                // Hazards describe the real source stack even when another
                // bucket already accounts for all of its quantity. A stack
                // reserved for caravan hauling can still catch fire.
                forbidden |= candidate.IsForbidden(Faction.OfPlayer);
                burning |= candidate.IsBurning();

                classifiedQuantities.TryGetValue(
                    candidate,
                    out int classifiedQuantity);
                int unclassified = StackQuantityAllocator.Remaining(
                    candidate.stackCount,
                    classifiedQuantity);
                if (unclassified <= 0)
                {
                    continue;
                }

                bool canReach = haulers.Any(pawn =>
                    pawn.CanReach(candidate, PathEndMode.Touch, Danger.Deadly));
                if (canReach)
                {
                    accessible += unclassified;
                }
                else
                {
                    inaccessible += unclassified;
                    navigationTarget ??= candidate;
                }
            }

            CargoCountLedger counts = CargoCountLedger.Create(
                requested,
                transferable.CountToTransfer,
                compatibleInventory,
                carried,
                reserved,
                accessible,
                inaccessible,
                blocked);

            if (navigationTarget == null)
            {
                navigationTarget = transferable.things.FirstOrDefault(
                    thing => thing != null && !thing.Destroyed);
            }

            return new CargoReadinessRow
            {
                Def = representative.GetInnerIfMinified().def,
                Label = transferable.LabelCap,
                Counts = counts,
                NavigationTarget = navigationTarget,
                HasForbidden = forbidden,
                HasBurning = burning
            };
        }

        private static void AddCargoProblems(
            FormationReadinessSnapshot snapshot,
            CargoReadinessRow row)
        {
            if (row.Counts.Unavailable > 0)
            {
                AddProblem(
                    snapshot,
                    "CR_ProblemUnavailable".Translate(row.Label),
                    "CR_ProblemUnavailableDetail".Translate(row.Counts.Unavailable),
                    ReadinessSeverity.Blocking,
                    row.NavigationTarget);
            }
            if (row.Counts.Inaccessible > 0)
            {
                AddProblem(
                    snapshot,
                    "CR_ProblemInaccessible".Translate(row.Label),
                    "CR_ProblemInaccessibleDetail".Translate(row.Counts.Inaccessible),
                    ReadinessSeverity.Blocking,
                    row.NavigationTarget);
            }
            if (row.Counts.Blocked > 0)
            {
                AddProblem(
                    snapshot,
                    "CR_ProblemReserved".Translate(row.Label),
                    "CR_ProblemReservedDetail".Translate(row.Counts.Blocked),
                    ReadinessSeverity.Warning,
                    row.NavigationTarget);
            }
            if (row.HasBurning)
            {
                AddProblem(
                    snapshot,
                    "CR_ProblemBurning".Translate(row.Label),
                    "CR_ProblemBurningDetail".Translate(),
                    ReadinessSeverity.Blocking,
                    row.NavigationTarget);
            }
            if (row.HasForbidden)
            {
                AddProblem(
                    snapshot,
                    "CR_ProblemForbidden".Translate(row.Label),
                    "CR_ProblemForbiddenDetail".Translate(),
                    ReadinessSeverity.Information,
                    row.NavigationTarget);
            }
        }

        private static void BuildMembers(
            FormationReadinessSnapshot snapshot,
            Lord lord,
            LordJob_FormAndSendCaravan job)
        {
            // Vanilla changes the meaningful destination once the formation
            // enters its leave toil; proximity to the packing spot is no longer
            // evidence that a member is ready at that stage.
            IntVec3 target = snapshot.MeetingPoint;
#if CARAVAN_READINESS_HAS_EXIT_SPOT
            if (lord.CurLordToil is LordToil_PrepareCaravan_Leave)
            {
                target = job.ExitSpot;
            }
#endif

            foreach (Pawn pawn in lord.ownedPawns
                .Concat(job.downedPawns ?? Enumerable.Empty<Pawn>())
                .Where(pawn => pawn != null)
                .Distinct()
                .OrderBy(pawn => pawn.thingIDNumber))
            {
                MemberReadinessRow row = ClassifyMember(pawn, target);
                if (pawn.RaceProps.Animal)
                {
                    snapshot.Animals.Add(row);
                }
                else
                {
                    snapshot.People.Add(row);
                }

                if (row.IsBlocking)
                {
                    AddProblem(
                        snapshot,
                        pawn.LabelShortCap,
                        row.Detail,
                        ReadinessSeverity.Blocking,
                        pawn);
                }
            }
        }

        private static MemberReadinessRow ClassifyMember(Pawn pawn, IntVec3 target)
        {
            string status;
            string detail;
            bool ready;
            bool blocking;

            if (pawn.Downed)
            {
                status = "CR_MemberDowned".Translate();
                detail = "CR_MemberDownedDetail".Translate();
                ready = false;
                blocking = true;
            }
            else if (pawn.InMentalState)
            {
                status = "CR_MemberMentalBreak".Translate();
                detail = "CR_MemberMentalBreakDetail".Translate();
                ready = false;
                blocking = true;
            }
            else if (!pawn.Spawned || pawn.Map == null)
            {
                status = "CR_MemberUnavailable".Translate();
                detail = "CR_MemberUnavailableDetail".Translate();
                ready = false;
                blocking = true;
            }
            else if (!pawn.CanReach(target, PathEndMode.OnCell, Danger.Deadly))
            {
                status = "CR_MemberNoPath".Translate();
                detail = "CR_MemberNoPathDetail".Translate();
                ready = false;
                blocking = true;
            }
            else if (!pawn.Position.InHorDistOf(target, 12f))
            {
                status = "CR_MemberApproaching".Translate();
                detail = "CR_MemberApproachingDetail".Translate();
                ready = false;
                blocking = false;
            }
#if CARAVAN_READINESS_HAS_ROPING
            else if (pawn.RaceProps.Animal &&
                     AnimalPenUtility.NeedsToBeManagedByRope(pawn) &&
                     !pawn.roping.IsRoped)
            {
                status = "CR_MemberWaitingRope".Translate();
                detail = "CR_MemberWaitingRopeDetail".Translate();
                ready = false;
                blocking = true;
            }
#endif
            else
            {
                status = "CR_MemberReady".Translate();
                detail = "CR_MemberReadyDetail".Translate();
                ready = true;
                blocking = false;
            }

            return new MemberReadinessRow
            {
                Pawn = pawn,
                Status = status,
                Detail = detail,
                Ready = ready,
                IsBlocking = blocking
            };
        }

        private static void AddCapacityProblem(
            FormationReadinessSnapshot snapshot,
            LordJob_FormAndSendCaravan job)
        {
            float capacityLeft = CaravanFormingUtility.CapacityLeft(job);
            if (capacityLeft < 0f)
            {
                AddProblem(
                    snapshot,
                    "CR_ProblemCapacity".Translate(),
                    "CR_ProblemCapacityDetail".Translate((-capacityLeft).ToStringMass()),
                    ReadinessSeverity.Blocking,
                    job.lord.ownedPawns.FirstOrDefault());
            }
        }

        private static void AddProblem(
            FormationReadinessSnapshot snapshot,
            string label,
            string detail,
            ReadinessSeverity severity,
            Thing target)
        {
            snapshot.Problems.Add(new ProblemReadinessRow
            {
                Label = label,
                Detail = detail,
                Severity = severity,
                NavigationTarget = target
            });
        }

        private static bool IsAvailableHauler(Pawn pawn)
        {
            return pawn != null &&
                   pawn.Spawned &&
                   !pawn.Downed &&
                   !pawn.InMentalState &&
                   pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation);
        }

        private static bool Matches(Thing representative, Thing candidate)
        {
            return representative != null &&
                   candidate != null &&
                   !candidate.Destroyed &&
                   TransferableUtility.TransferAsOne(
                       representative,
                       candidate,
                       TransferAsOneMode.PodsOrCaravanPacking);
        }
    }
}
