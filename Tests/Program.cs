using System;
using System.Collections.Generic;
using CaravanReadiness.Domain;

namespace CaravanReadiness.Tests
{
    internal static class Program
    {
        private static int assertions;

        private static int Main()
        {
            LoadedAndRemainingAreStable();
            InFlightCountsAreSubsetsOfRemaining();
            ProblemBucketsAreCappedWithoutOverlap();
            MissingSourcesBecomeUnavailable();
            ReducedManifestDoesNotCreatePhantomLoadedCargo();
            DroppedLoadedCargoReducesEffectiveRequest();
            FormationOrderIsIndependentOfEnumerationOrder();
            PartialReservationLeavesWaitingRemainder();
            MultipleReservationsShareOneStackWithoutOverlap();
            ReservationAllocationCapsAtStackSize();
            ManifestSlotsFollowDefinitionsAcrossReorder();
            ManifestSlotsHandleStructuralAddAndRemove();
            DuplicateDefinitionsFollowTransferGroupIdentity();
            MinifiedIdentitySurvivesRefreshAndLoad();

            Console.WriteLine($"PASS: {assertions} Caravan Readiness assertions");
            return 0;
        }

        private static void LoadedAndRemainingAreStable()
        {
            CargoCountLedger ledger = CargoCountLedger.Create(
                initialRequested: 50,
                remaining: 13,
                compatibleInventory: 37,
                carried: 0,
                reserved: 0,
                accessible: 13,
                inaccessible: 0,
                blocked: 0);
            Equal(50, ledger.Requested, "requested total");
            Equal(37, ledger.Loaded, "loaded total");
            Equal(13, ledger.Remaining, "remaining total");
            Equal(13, ledger.Waiting, "waiting total");
        }

        private static void InFlightCountsAreSubsetsOfRemaining()
        {
            CargoCountLedger ledger = CargoCountLedger.Create(
                20, 12, 8, carried: 3, reserved: 4,
                accessible: 5, inaccessible: 0, blocked: 0);
            Equal(3, ledger.Carried, "carried subset");
            Equal(4, ledger.Reserved, "reserved subset");
            Equal(5, ledger.Waiting, "waiting subset");
            Equal(12,
                ledger.Carried + ledger.Reserved + ledger.Waiting,
                "remaining buckets exactly reconcile");
        }

        private static void ProblemBucketsAreCappedWithoutOverlap()
        {
            CargoCountLedger ledger = CargoCountLedger.Create(
                10, 7, 3, carried: 2, reserved: 2,
                accessible: 4, inaccessible: 4, blocked: 4);
            Equal(2, ledger.Carried, "carried priority");
            Equal(2, ledger.Reserved, "reservation priority");
            Equal(3, ledger.Blocked, "blocked capped");
            Equal(0, ledger.Inaccessible, "no double-count inaccessible");
            Equal(0, ledger.Waiting, "no double-count waiting");
        }

        private static void MissingSourcesBecomeUnavailable()
        {
            CargoCountLedger ledger = CargoCountLedger.Create(
                8, 5, 3, carried: 0, reserved: 0,
                accessible: 2, inaccessible: 1, blocked: 0);
            Equal(2, ledger.Unavailable, "destroyed or missing source units");
            Equal(1, ledger.Inaccessible, "inaccessible source units");
        }

        private static void ReducedManifestDoesNotCreatePhantomLoadedCargo()
        {
            CargoCountLedger ledger = CargoCountLedger.Create(
                initialRequested: 20,
                remaining: 7,
                compatibleInventory: 5,
                carried: 0,
                reserved: 0,
                accessible: 7,
                inaccessible: 0,
                blocked: 0);
            Equal(12, ledger.Requested, "effective request after removal");
            Equal(5, ledger.Loaded, "actual compatible inventory caps loaded");
        }

        private static void DroppedLoadedCargoReducesEffectiveRequest()
        {
            CargoCountLedger ledger = CargoCountLedger.Create(
                initialRequested: 10,
                remaining: 4,
                compatibleInventory: 3,
                carried: 0,
                reserved: 0,
                accessible: 4,
                inaccessible: 0,
                blocked: 0);
            Equal(7, ledger.Requested, "dropped loaded cargo request");
            Equal(3, ledger.Loaded, "dropped loaded cargo count");
        }

        private static void FormationOrderIsIndependentOfEnumerationOrder()
        {
            List<int> loadIds = new List<int> { 42, 7, 19 };
            loadIds.Sort(FormationOrdering.CompareLoadIds);
            Equal(7, loadIds[0], "stable first formation");
            Equal(19, loadIds[1], "stable second formation");
            Equal(42, loadIds[2], "stable third formation");
        }

        private static void PartialReservationLeavesWaitingRemainder()
        {
            int reserved = StackQuantityAllocator.Allocate(20, 0, 5);
            Equal(5, reserved, "partial reservation allocation");
            Equal(15,
                StackQuantityAllocator.Remaining(20, reserved),
                "partial reservation waiting remainder");
        }

        private static void MultipleReservationsShareOneStackWithoutOverlap()
        {
            int first = StackQuantityAllocator.Allocate(10, 0, 3);
            int second = StackQuantityAllocator.Allocate(10, first, 4);
            Equal(3, first, "first reserver allocation");
            Equal(4, second, "second reserver allocation");
            Equal(3,
                StackQuantityAllocator.Remaining(10, first + second),
                "multi-reserver waiting remainder");
        }

        private static void ReservationAllocationCapsAtStackSize()
        {
            int first = StackQuantityAllocator.Allocate(6, 0, 4);
            int second = StackQuantityAllocator.Allocate(6, first, 8);
            Equal(4, first, "capped first allocation");
            Equal(2, second, "capped second allocation");
            Equal(0,
                StackQuantityAllocator.Remaining(6, first + second),
                "fully allocated stack has no remainder");
        }

        private static void ManifestSlotsFollowDefinitionsAcrossReorder()
        {
            int[] matches = ManifestSlotReconciler.Match(
                new[] { "Steel", "WoodLog", "MedicineIndustrial" },
                new[] { "MedicineIndustrial", "Steel", "WoodLog" });
            Equal(2, matches[0], "reordered medicine identity");
            Equal(0, matches[1], "reordered steel identity");
            Equal(1, matches[2], "reordered wood identity");
        }

        private static void ManifestSlotsHandleStructuralAddAndRemove()
        {
            int[] matches = ManifestSlotReconciler.Match(
                new[] { "Steel", "WoodLog" },
                new[] { "ComponentIndustrial", "Steel" });
            Equal(ManifestSlotReconciler.NewSlot,
                matches[0],
                "new manifest definition gets a new baseline");
            Equal(0,
                matches[1],
                "retained definition preserves its baseline");
            Equal(2, matches.Length, "removed definition drops its slot");
        }

        private static void DuplicateDefinitionsFollowTransferGroupIdentity()
        {
            int[] matches = ManifestSlotReconciler.Match(
                new[]
                {
                    "outer=Apparel_Cape|quality=2",
                    "outer=Apparel_Cape|quality=5",
                    "outer=Steel"
                },
                new[]
                {
                    "outer=Apparel_Cape|quality=5",
                    "outer=Steel"
                });
            Equal(1, matches[0],
                "duplicate ThingDef retains the correct quality group");
            Equal(2, matches[1],
                "remove and reorder retains the unrelated group");
            Equal(2, matches.Length,
                "removed duplicate transfer group drops only its own slot");
        }

        private static void MinifiedIdentitySurvivesRefreshAndLoad()
        {
            const string shelf =
                "outer=MinifiedThing|inner=Shelf|stuff=Steel";
            const string stool =
                "outer=MinifiedThing|inner=Stool|stuff=WoodLog";
            int[] refreshed = ManifestSlotReconciler.Match(
                new[] { shelf, stool },
                new[] { stool, shelf });
            Equal(1, refreshed[0],
                "minified inner definition survives refresh reorder");
            Equal(0, refreshed[1],
                "minified outer definition does not collapse distinct rows");

            int[] loaded = ManifestSlotReconciler.Match(
                new[] { stool, shelf },
                new[] { stool, shelf });
            Equal(0, loaded[0],
                "serialized minified stool identity survives load");
            Equal(1, loaded[1],
                "serialized minified shelf identity survives load");
        }

        private static void Equal<T>(T expected, T actual, string scenario)
        {
            assertions++;
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException(
                    $"{scenario}: expected {expected}, actual {actual}");
            }
        }
    }
}
