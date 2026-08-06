using System;
using System.Collections.Generic;
using System.Linq;
using CaravanReadiness.Domain;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace CaravanReadiness.State
{
    /// <summary>
    /// Persists parallel observations for one vanilla formation so unavailable
    /// or reordered transfer rows retain their original request history.
    /// </summary>
    public sealed class FormationBaselineRecord : IExposable
    {
        public int LordLoadId;
        public List<int> RequestedCounts = new List<int>();
        public List<int> RemainingCounts = new List<int>();
        public List<int> InventoryCounts = new List<int>();
        public List<ThingDef> ThingDefs = new List<ThingDef>();
        public List<string> TransferGroupKeys = new List<string>();
        public List<Thing> TransferGroupRepresentatives = new List<Thing>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref LordLoadId, "lordLoadId");
            Scribe_Collections.Look(ref RequestedCounts, "requestedCounts", LookMode.Value);
            Scribe_Collections.Look(ref RemainingCounts, "remainingCounts", LookMode.Value);
            Scribe_Collections.Look(ref InventoryCounts, "inventoryCounts", LookMode.Value);
            Scribe_Collections.Look(ref ThingDefs, "thingDefs", LookMode.Def);
            Scribe_Collections.Look(ref TransferGroupKeys, "transferGroupKeys", LookMode.Value);
            Scribe_Collections.Look(
                ref TransferGroupRepresentatives,
                "transferGroupRepresentatives",
                LookMode.Reference);
            RequestedCounts ??= new List<int>();
            RemainingCounts ??= new List<int>();
            InventoryCounts ??= new List<int>();
            ThingDefs ??= new List<ThingDef>();
            TransferGroupKeys ??= new List<string>();
            TransferGroupRepresentatives ??= new List<Thing>();
        }
    }

    /// <summary>
    /// Owns map-scoped manifest baselines and reconciles them against vanilla
    /// lords without introducing a second caravan formation state machine.
    /// </summary>
    public sealed class FormationBaselineComponent : MapComponent
    {
        private List<FormationBaselineRecord> records =
            new List<FormationBaselineRecord>();

        public FormationBaselineComponent(Map map)
            : base(map)
        {
        }

        internal int RecordCount => records.Count;

        public void Capture(Lord lord, LordJob_FormAndSendCaravan job)
        {
            if (lord == null || job?.transferables == null)
            {
                return;
            }

            FormationBaselineRecord record = records.FirstOrDefault(
                item => item.LordLoadId == lord.loadID);
            if (record == null)
            {
                record = new FormationBaselineRecord { LordLoadId = lord.loadID };
                records.Add(record);
            }

            record.RequestedCounts = job.transferables
                .Select(item => item?.CountToTransfer ?? 0)
                .ToList();
            record.RemainingCounts = job.transferables
                .Select(item => item?.CountToTransfer ?? 0)
                .ToList();
            IEnumerable<Pawn> carriers = lord.ownedPawns
                .Concat(job.downedPawns ?? Enumerable.Empty<Pawn>());
            record.InventoryCounts = job.transferables
                .Select(item => CargoInventoryCounter.Count(item, carriers))
                .ToList();
            record.ThingDefs = job.transferables
                .Select(TransferGroupIdentity.DisplayDefinitionFor)
                .ToList();
            record.TransferGroupKeys = job.transferables
                .Select(TransferGroupIdentity.SignatureFor)
                .ToList();
            record.TransferGroupRepresentatives = job.transferables
                .Select(item => item?.AnyThing)
                .ToList();
        }

        public int RowCountFor(Lord lord)
        {
            FormationBaselineRecord record = records.FirstOrDefault(
                item => item.LordLoadId == lord.loadID);
            return record?.RequestedCounts?.Count ?? 0;
        }

        public void ReconcileTransferSlots(
            Lord lord,
            IReadOnlyList<TransferableOneWay> currentTransferables)
        {
            FormationBaselineRecord record = records.FirstOrDefault(
                item => item.LordLoadId == lord.loadID);
            if (record == null || currentTransferables == null)
            {
                return;
            }

            List<ThingDef> previousDefinitions = record.ThingDefs.ToList();
            List<string> currentKeys = currentTransferables
                .Select(TransferGroupIdentity.SignatureFor)
                .ToList();
            List<Thing> currentRepresentatives = currentTransferables
                .Select(item => item?.AnyThing)
                .ToList();
            List<ThingDef> currentDefinitions = currentTransferables
                .Select(TransferGroupIdentity.DisplayDefinitionFor)
                .ToList();

            MigrateLegacyIdentity(record, currentKeys, currentRepresentatives);
            int[] matches = ManifestSlotReconciler.Match(
                record.TransferGroupKeys,
                currentKeys);
            // Signatures can legitimately collide for transfer groups that
            // vanilla still keeps separate, so live representatives refine the
            // structural match before all parallel lists are rebuilt.
            RefineMatchesWithRepresentatives(
                record.TransferGroupRepresentatives,
                currentRepresentatives,
                matches);

            List<int> requested = new List<int>(matches.Length);
            List<int> remaining = new List<int>(matches.Length);
            List<int> inventory = new List<int>(matches.Length);
            List<ThingDef> definitions =
                new List<ThingDef>(matches.Length);
            List<string> keys = new List<string>(matches.Length);
            List<Thing> representatives = new List<Thing>(matches.Length);
            for (int index = 0; index < matches.Length; index++)
            {
                int previousIndex = matches[index];
                bool existing = previousIndex >= 0;
                requested.Add(existing
                    ? ValueAt(record.RequestedCounts, previousIndex)
                    : 0);
                remaining.Add(existing
                    ? ValueAt(record.RemainingCounts, previousIndex)
                    : 0);
                inventory.Add(existing
                    ? ValueAt(record.InventoryCounts, previousIndex)
                    : 0);
                definitions.Add(
                    currentDefinitions[index] ??
                    (existing && previousIndex < previousDefinitions.Count
                        ? previousDefinitions[previousIndex]
                        : null));
                keys.Add(currentKeys[index] ??
                    (existing && previousIndex < record.TransferGroupKeys.Count
                        ? record.TransferGroupKeys[previousIndex]
                        : null));
                representatives.Add(currentRepresentatives[index] ??
                    (existing && previousIndex <
                        record.TransferGroupRepresentatives.Count
                        ? record.TransferGroupRepresentatives[previousIndex]
                        : null));
            }

            record.RequestedCounts = requested;
            record.RemainingCounts = remaining;
            record.InventoryCounts = inventory;
            record.ThingDefs = definitions;
            record.TransferGroupKeys = keys;
            record.TransferGroupRepresentatives = representatives;
        }

        public ThingDef DefinitionFor(Lord lord, int transferableIndex)
        {
            FormationBaselineRecord record = records.FirstOrDefault(
                item => item.LordLoadId == lord.loadID);
            return record != null &&
                   transferableIndex >= 0 &&
                   transferableIndex < record.ThingDefs.Count
                ? record.ThingDefs[transferableIndex]
                : null;
        }

        public int RemainingFor(Lord lord, int transferableIndex)
        {
            FormationBaselineRecord record = records.FirstOrDefault(
                item => item.LordLoadId == lord.loadID);
            return record != null &&
                   transferableIndex >= 0 &&
                   transferableIndex < record.RemainingCounts.Count
                ? record.RemainingCounts[transferableIndex]
                : 0;
        }

        public int ObserveRequestedFor(
            Lord lord,
            int transferableIndex,
            int currentRemaining,
            int currentInventory)
        {
            FormationBaselineRecord record = records.FirstOrDefault(
                item => item.LordLoadId == lord.loadID);
            if (record == null || transferableIndex < 0)
            {
                return Math.Max(0, currentRemaining) +
                    Math.Max(0, currentInventory);
            }

            while (record.RequestedCounts.Count <= transferableIndex)
            {
                record.RequestedCounts.Add(
                    Math.Max(0, currentRemaining) +
                    Math.Max(0, currentInventory));
            }
            while (record.RemainingCounts.Count <= transferableIndex)
            {
                record.RemainingCounts.Add(Math.Max(0, currentRemaining));
            }
            while (record.InventoryCounts.Count <= transferableIndex)
            {
                record.InventoryCounts.Add(Math.Max(0, currentInventory));
            }
            int requested = ManifestRequestTracker.Observe(
                record.RequestedCounts[transferableIndex],
                record.RemainingCounts[transferableIndex],
                record.InventoryCounts[transferableIndex],
                currentRemaining,
                currentInventory);
            record.RequestedCounts[transferableIndex] = requested;
            record.RemainingCounts[transferableIndex] =
                Math.Max(0, currentRemaining);
            record.InventoryCounts[transferableIndex] =
                Math.Max(0, currentInventory);
            return requested;
        }

        private static int ValueAt(List<int> values, int index)
        {
            return values != null && index >= 0 && index < values.Count
                ? values[index]
                : 0;
        }

        private static void MigrateLegacyIdentity(
            FormationBaselineRecord record,
            IReadOnlyList<string> currentKeys,
            IReadOnlyList<Thing> currentRepresentatives)
        {
            int slotCount = record.RequestedCounts.Count;
            if (record.TransferGroupKeys.Count == slotCount &&
                record.TransferGroupRepresentatives.Count == slotCount)
            {
                return;
            }

            // Saves from before stable group identity existed have only
            // positional history. Seed identities from the current vanilla
            // order once so later refreshes can use structural reconciliation.
            record.TransferGroupKeys = new List<string>(slotCount);
            record.TransferGroupRepresentatives = new List<Thing>(slotCount);
            for (int index = 0; index < slotCount; index++)
            {
                record.TransferGroupKeys.Add(index < currentKeys.Count
                    ? currentKeys[index]
                    : null);
                record.TransferGroupRepresentatives.Add(
                    index < currentRepresentatives.Count
                        ? currentRepresentatives[index]
                        : null);
            }
        }

        private static void RefineMatchesWithRepresentatives(
            IReadOnlyList<Thing> previous,
            IReadOnlyList<Thing> current,
            int[] matches)
        {
            bool[] used = new bool[previous?.Count ?? 0];
            int[] exactMatches = new int[matches.Length];
            for (int index = 0; index < matches.Length; index++)
            {
                Thing currentThing = index < current.Count ? current[index] : null;
                int exact = FindRepresentativeMatch(previous, currentThing, used);
                exactMatches[index] = exact;
                if (exact >= 0)
                {
                    used[exact] = true;
                }
            }

            for (int index = 0; index < matches.Length; index++)
            {
                int exact = exactMatches[index];
                if (exact >= 0)
                {
                    matches[index] = exact;
                    continue;
                }
                int fallback = matches[index];
                if (fallback >= 0 && fallback < used.Length && !used[fallback])
                {
                    used[fallback] = true;
                }
                else if (index < current.Count && current[index] != null)
                {
                    matches[index] = ManifestSlotReconciler.NewSlot;
                }
            }
        }

        private static int FindRepresentativeMatch(
            IReadOnlyList<Thing> previous,
            Thing current,
            bool[] used)
        {
            if (previous == null || current == null)
            {
                return ManifestSlotReconciler.NewSlot;
            }

            for (int index = 0; index < previous.Count; index++)
            {
                Thing candidate = previous[index];
                if (!used[index] && candidate != null &&
                    !candidate.Destroyed &&
                    TransferableUtility.TransferAsOne(
                        candidate,
                        current,
                        TransferAsOneMode.PodsOrCaravanPacking))
                {
                    return index;
                }
            }
            return ManifestSlotReconciler.NewSlot;
        }

        public void Remove(int lordLoadId)
        {
            records.RemoveAll(item => item.LordLoadId == lordLoadId);
        }

        public void ReconcileActiveLords()
        {
            HashSet<int> active = new HashSet<int>(
                map.lordManager.lords
                    .Where(lord => lord.LordJob is LordJob_FormAndSendCaravan)
                    .Select(lord => lord.loadID));
            records.RemoveAll(item => !active.Contains(item.LordLoadId));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref records, "formationBaselines", LookMode.Deep);
            records ??= new List<FormationBaselineRecord>();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                records.RemoveAll(item => item == null);
            }
        }
    }
}
