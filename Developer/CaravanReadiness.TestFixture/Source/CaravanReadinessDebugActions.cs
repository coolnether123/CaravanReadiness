using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CaravanReadiness.Domain;
using CaravanReadiness.State;
using CaravanReadiness.UI;
using LudeonTK;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace CaravanReadiness.Diagnostics
{
    internal static class CaravanReadinessDebugActions
    {
        private const string LogPrefix = "[Caravan Readiness] ";
        private static int spotThingId = -1;
        private static int preferredLordLoadId = -1;

        [DebugAction(
            "Caravan Readiness",
            "Create real verification formation",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void CreateRealVerificationFormation()
        {
            try
            {
                Map map = RequireMap();
                Building spot = FindOrCreatePackingSpot(map);
                List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned
                    .Where(pawn => pawn.GetLord()?.LordJob is not LordJob_FormAndSendCaravan)
                    .OrderBy(pawn => pawn.thingIDNumber)
                    .Take(2)
                    .ToList();
                Require(colonists.Count > 0, "no free colonist is available");

                List<TransferableOneWay> transferables = new List<TransferableOneWay>
                {
                    CreateTransferable(map, spot.Position, "Steel", 60, 40, 6),
                    CreateTransferable(map, spot.Position, "WoodLog", 35, 20, 8),
                    CreateTransferable(map, spot.Position, "MedicineIndustrial", 12, 8, 10),
                    CreateTransferable(map, spot.Position, "Silver", 25, 15, 100),
                    CreateTransferable(map, spot.Position, "Cloth", 20, 10, 120)
                };
                IntVec3 exitSpot;
                Require(
                    RCellFinder.TryFindClosestEdgeCellTo(
                        spot.Position,
                        map,
                        out exitSpot),
                    "vanilla could not resolve an exit spot");

                CaravanFormingUtility.StartFormingCaravan(
                    colonists,
                    new List<Pawn>(),
                    Faction.OfPlayer,
                    transferables,
                    spot.Position,
                    exitSpot,
                    map.Tile,
                    map.Tile);

                Lord lord = map.lordManager.lords
                    .Where(candidate =>
                        candidate.LordJob is LordJob_FormAndSendCaravan &&
                        candidate.ownedPawns.Contains(colonists[0]))
                    .OrderByDescending(candidate => candidate.loadID)
                    .FirstOrDefault();
                Require(lord != null, "vanilla did not create a caravan lord");
                preferredLordLoadId = lord.loadID;
                spotThingId = spot.thingIDNumber;
                Find.Selector.ClearSelection();
                Find.Selector.Select(spot);
                OpenSpotWindow(spot);
                LogSnapshot("created", lord);
            }
            catch (Exception exception)
            {
                Log.Error(LogPrefix + "verificationCreate=fail " + exception);
            }
        }

        [DebugAction(
            "Caravan Readiness",
            "Open verification readiness window",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void OpenVerificationReadinessWindow()
        {
            try
            {
                Building spot = RequireSpot(RequireMap());
                Find.Selector.ClearSelection();
                Find.Selector.Select(spot);
                OpenSpotWindow(spot);
                LogSnapshot("opened", RequireFormation(spot.Map));
            }
            catch (Exception exception)
            {
                Log.Error(LogPrefix + "verificationOpen=fail " + exception);
            }
        }

        [DebugAction(
            "Caravan Readiness",
            "Open verification cargo window",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void OpenVerificationCargoWindow()
        {
            try
            {
                Building spot = RequireSpot(RequireMap());
                Lord lord = RequireFormation(spot.Map);
                Find.Selector.ClearSelection();
                Find.Selector.Select(spot);
                Find.WindowStack.Add(new Dialog_CaravanReadiness(
                    spot.Map,
                    spot.Position,
                    lord.loadID,
                    ReadinessSection.Cargo));
                LogSnapshot("cargoWindowOpened", lord);
            }
            catch (Exception exception)
            {
                Log.Error(LogPrefix + "verificationCargoWindow=fail " + exception);
            }
        }

        [DebugAction(
            "Caravan Readiness",
            "Open narrow verification cargo window",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void OpenNarrowVerificationCargoWindow()
        {
            try
            {
                Building spot = RequireSpot(RequireMap());
                Lord lord = RequireFormation(spot.Map);
                Find.Selector.ClearSelection();
                Find.Selector.Select(spot);
                Dialog_CaravanReadiness dialog =
                    new Dialog_CaravanReadiness(
                        spot.Map,
                        spot.Position,
                        lord.loadID,
                        ReadinessSection.Cargo);
                Find.WindowStack.Add(dialog);
                dialog.windowRect.width =
                    Dialog_CaravanReadiness.MinimumWindowWidth;
                dialog.windowRect.height =
                    Dialog_CaravanReadiness.MinimumWindowHeight;
                dialog.windowRect.x =
                    (Verse.UI.screenWidth - dialog.windowRect.width) / 2f;
                dialog.windowRect.y =
                    (Verse.UI.screenHeight - dialog.windowRect.height) / 2f;
                LogSnapshot("narrowCargoWindowOpened", lord);
            }
            catch (Exception exception)
            {
                Log.Error(LogPrefix +
                    "verificationNarrowCargoWindow=fail " + exception);
            }
        }

        [DebugAction(
            "Caravan Readiness",
            "Verify problem navigation",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void VerifyProblemNavigation()
        {
            try
            {
                Lord lord = RequireFormation(RequireMap());
                ProblemReadinessRow problem = ReadinessSnapshotBuilder.Build(lord)
                    .Problems
                    .FirstOrDefault(row => row.NavigationTarget != null);
                Require(problem != null, "no navigable problem row is available");
                Dialog_CaravanReadiness.NavigateTo(problem.NavigationTarget);
                Require(
                    Find.Selector.IsSelected(problem.NavigationTarget),
                    "the shared row navigation path did not select its target");
                Log.Message(LogPrefix + "verificationNavigation=complete target=" +
                    problem.NavigationTarget.GetUniqueLoadID());
            }
            catch (Exception exception)
            {
                Log.Error(LogPrefix + "verificationNavigation=fail " + exception);
            }
        }

        [DebugAction(
            "Caravan Readiness",
            "Stage carried cargo",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void StageCarriedCargo()
        {
            try
            {
                Lord lord = RequireFormation(RequireMap());
                LordJob_FormAndSendCaravan formation =
                    (LordJob_FormAndSendCaravan)lord.LordJob;
                TransferableOneWay row = RequiredRow(formation, "Steel");
                Thing source = row.things.First(thing => thing.Spawned);
                Pawn pawn = lord.ownedPawns
                    .Where(member => member.IsColonist)
                    .OrderBy(member => member.thingIDNumber)
                    .First();
                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
                Job job = JobMaker.MakeJob(
                    JobDefOf.PrepareCaravan_GatherItems,
                    source);
                job.lord = lord;
                pawn.jobs.StartJob(job, JobCondition.InterruptForced);
                int carried = pawn.carryTracker.TryStartCarry(source, 7, false);
                Require(carried == 7, "could not put seven units in the carry tracker");
                Thing carriedThing = pawn.carryTracker.CarriedThing;
                if (!row.things.Contains(carriedThing))
                {
                    row.things.Add(carriedThing);
                }
                CargoReadinessRow snapshotRow = RequiredSnapshotRow(
                    ReadinessSnapshotBuilder.Build(lord),
                    "Steel");
                Require(
                    snapshotRow.Counts.Carried == 7,
                    "the snapshot did not report seven carried Steel");
                LogSnapshot("carried", lord);
            }
            catch (Exception exception)
            {
                Log.Error(LogPrefix + "verificationCarried=fail " + exception);
            }
        }

        [DebugAction(
            "Caravan Readiness",
            "Stage loaded cargo",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void StageLoadedCargo()
        {
            try
            {
                Lord lord = RequireFormation(RequireMap());
                LordJob_FormAndSendCaravan formation =
                    (LordJob_FormAndSendCaravan)lord.LordJob;
                TransferableOneWay row = RequiredRow(formation, "Steel");
                Pawn pawn = lord.ownedPawns
                    .Where(member => member.IsColonist)
                    .OrderBy(member => member.thingIDNumber)
                    .First();
                Thing carried = pawn.carryTracker.CarriedThing;
                Require(carried != null, "the verification pawn is not carrying cargo");
                int count = carried.stackCount;
                pawn.carryTracker.innerContainer.TryTransferToContainer(
                    carried,
                    pawn.inventory.innerContainer,
                    count,
                    out Thing transferred);
                Require(transferred != null, "carried cargo did not enter inventory");
                row.AdjustTo(Math.Max(0, row.CountToTransfer - count));
                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
                CargoReadinessRow snapshotRow = RequiredSnapshotRow(
                    ReadinessSnapshotBuilder.Build(lord),
                    "Steel");
                Require(
                    snapshotRow.Counts.Loaded == 7 &&
                    snapshotRow.Counts.Requested == 40,
                    "the snapshot did not preserve 7 / 40 loaded Steel");
                LogSnapshot("loaded", lord);
            }
            catch (Exception exception)
            {
                Log.Error(LogPrefix + "verificationLoaded=fail " + exception);
            }
        }

        [DebugAction(
            "Caravan Readiness",
            "Stage cargo problems",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void StageCargoProblems()
        {
            try
            {
                Map map = RequireMap();
                Lord lord = RequireFormation(map);
                LordJob_FormAndSendCaravan formation =
                    (LordJob_FormAndSendCaravan)lord.LordJob;

                TransferableOneWay wood = RequiredRow(formation, "WoodLog");
                Thing reservedTarget = RequiredSpawnedThing(wood, "WoodLog");
                List<Pawn> outsiders = map.mapPawns.FreeColonistsSpawned
                    .Where(pawn => !lord.ownedPawns.Contains(pawn))
                    .OrderBy(pawn => pawn.thingIDNumber)
                    .Take(2)
                    .ToList();
                while (outsiders.Count < 2)
                {
                    Pawn generated = PawnGenerator.GeneratePawn(
                        PawnKindDefOf.Colonist,
                        Faction.OfPlayer);
                    GenSpawn.Spawn(
                        generated,
                        CellFinder.RandomSpawnCellForPawnNear(
                            reservedTarget.Position,
                            map),
                        map);
                    outsiders.Add(generated);
                }
                int[] reservationCounts = { 3, 4 };
                for (int index = 0; index < outsiders.Count; index++)
                {
                    Pawn outsider = outsiders[index];
                    Job reserveJob = JobMaker.MakeJob(JobDefOf.Wait);
                    reserveJob.expiryInterval = 60000;
                    outsider.jobs.EndCurrentJob(JobCondition.InterruptForced);
                    outsider.jobs.StartJob(
                        reserveJob,
                        JobCondition.InterruptForced);
                    Require(
                        outsider.Reserve(
                            reservedTarget,
                            reserveJob,
                            2,
                            reservationCounts[index],
                            null,
                            true),
                        "vanilla partial-stack reservation failed");
                }

                TransferableOneWay medicine = RequiredRow(
                    formation,
                    "MedicineIndustrial");
                Thing unavailable = RequiredSpawnedThing(
                    medicine,
                    "MedicineIndustrial");
                unavailable.Destroy(DestroyMode.Vanish);

                TransferableOneWay silver = RequiredRow(formation, "Silver");
                Thing inaccessible = RequiredSpawnedThing(silver, "Silver");
                List<IntVec3> enclosureCells = GenAdj.AdjacentCells
                    .Select(offset => inaccessible.Position + offset)
                    .ToList();
                Require(
                    enclosureCells.All(cell =>
                        cell.InBounds(map) &&
                        cell.GetEdifice(map) == null &&
                        !cell.GetThingList(map).Any(thing => thing is Pawn)),
                    "the silver fixture cell cannot be safely enclosed");
                ThingDef wallDefinition = ThingDefOf.Wall;
                ThingDef wallStuff = GenStuff.DefaultStuffFor(wallDefinition);
                foreach (IntVec3 cell in enclosureCells)
                {
                    Building wall = (Building)ThingMaker.MakeThing(
                        wallDefinition,
                        wallStuff);
                    wall.SetFaction(Faction.OfPlayer);
                    GenSpawn.Spawn(wall, cell, map, WipeMode.Vanish);
                }

                TransferableOneWay cloth = RequiredRow(formation, "Cloth");
                Thing burning = RequiredSpawnedThing(cloth, "Cloth");
                Require(
                    FireUtility.TryStartFireIn(
                        burning.Position,
                        map,
                        0.2f,
                    null),
                    "vanilla did not ignite the cloth fixture");
                FormationReadinessSnapshot snapshot =
                    ReadinessSnapshotBuilder.Build(lord);
                Require(
                    RequiredSnapshotRow(snapshot, "MedicineIndustrial")
                        .Counts.Unavailable == 8,
                    "the snapshot did not report eight unavailable medicine");
                Require(
                    RequiredSnapshotRow(snapshot, "Silver")
                        .Counts.Inaccessible == 15,
                    "the snapshot did not report fifteen inaccessible Silver");
                Require(
                    RequiredSnapshotRow(snapshot, "WoodLog")
                        .Counts.Blocked == 7 &&
                    RequiredSnapshotRow(snapshot, "WoodLog")
                        .Counts.Waiting ==
                    RequiredSnapshotRow(snapshot, "WoodLog")
                        .Counts.Remaining - 7,
                    "the snapshot did not reconcile two partial Wood reservations");
                Require(
                    RequiredSnapshotRow(snapshot, "Cloth").HasBurning,
                    "the snapshot did not report burning Cloth");
                LogSnapshot("problems", lord);
            }
            catch (Exception exception)
            {
                Log.Error(LogPrefix + "verificationProblems=fail " + exception);
            }
        }

        [DebugAction(
            "Caravan Readiness",
            "Change verification manifest",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ChangeVerificationManifest()
        {
            try
            {
                Lord lord = RequireFormation(RequireMap());
                LordJob_FormAndSendCaravan formation =
                    (LordJob_FormAndSendCaravan)lord.LordJob;
                TransferableOneWay steel = RequiredRow(formation, "Steel");
                steel.AdjustTo(Math.Min(steel.MaxCount, steel.CountToTransfer + 3));
                TransferableOneWay wood = RequiredRow(formation, "WoodLog");
                wood.AdjustTo(Math.Max(0, wood.CountToTransfer - 4));
                FormationReadinessSnapshot snapshot =
                    ReadinessSnapshotBuilder.Build(lord);
                CargoReadinessRow steelSnapshot = RequiredSnapshotRow(
                    snapshot,
                    "Steel");
                Require(
                    steelSnapshot.Counts.Loaded == 7 &&
                    steelSnapshot.Counts.Requested == 43,
                    "the edited Steel manifest did not report 7 / 43");
                Require(
                    RequiredSnapshotRow(snapshot, "WoodLog")
                        .Counts.Requested == 16,
                    "the edited Wood manifest did not report sixteen requested");
                LogSnapshot("manifestChanged", lord);
            }
            catch (Exception exception)
            {
                Log.Error(LogPrefix + "verificationManifest=fail " + exception);
            }
        }

        [DebugAction(
            "Caravan Readiness",
            "Verify structural manifest changes",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void VerifyStructuralManifestChanges()
        {
            try
            {
                Map map = RequireMap();
                Building spot = RequireSpot(map);
                Lord lord = RequireFormation(map);
                LordJob_FormAndSendCaravan formation =
                    (LordJob_FormAndSendCaravan)lord.LordJob;
                TransferableOneWay steel = RequiredRow(formation, "Steel");
                TransferableOneWay wood = RequiredRow(formation, "WoodLog");
                TransferableOneWay cloth = RequiredRow(formation, "Cloth");
                formation.transferables.Remove(cloth);
                formation.transferables.Remove(wood);
                formation.transferables.Insert(0, wood);
                formation.transferables.Remove(steel);
                formation.transferables.Insert(1, steel);
                formation.transferables.Add(CreateTransferable(
                    map,
                    spot.Position,
                    "ComponentIndustrial",
                    8,
                    6,
                    150));

                FormationReadinessSnapshot snapshot =
                    ReadinessSnapshotBuilder.Build(lord);
                Require(
                    RequiredSnapshotRow(snapshot, "Steel")
                        .Counts.Requested == 40,
                    "reordering changed the Steel baseline identity");
                Require(
                    RequiredSnapshotRow(snapshot, "WoodLog")
                        .Counts.Requested == 20,
                    "reordering changed the Wood baseline identity");
                Require(
                    RequiredSnapshotRow(snapshot, "ComponentIndustrial")
                        .Counts.Requested == 6,
                    "the structurally added row did not get a new baseline");
                Require(
                    snapshot.Cargo.All(row => row.Def?.defName != "Cloth"),
                    "the structurally removed row retained a stale baseline");
                Log.Message(LogPrefix +
                    "verificationStructuralManifest=complete rows=" +
                    snapshot.Cargo.Count +
                    " orderedDefs=" +
                    string.Join(",", snapshot.Cargo.Select(row => row.Def?.defName)));
            }
            catch (Exception exception)
            {
                Log.Error(LogPrefix +
                    "verificationStructuralManifest=fail " + exception);
            }
        }

        [DebugAction(
            "Caravan Readiness",
            "Stage transfer identity regression",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void StageTransferIdentityRegression()
        {
            try
            {
                Map map = RequireMap();
                Building spot = RequireSpot(map);
                Lord lord = RequireFormation(map);
                LordJob_FormAndSendCaravan formation =
                    (LordJob_FormAndSendCaravan)lord.LordJob;

                TransferableOneWay meatMeal = CreateIngredientMeal(
                    map,
                    spot.Position,
                    "Meat_Human",
                    10,
                    3,
                    170);
                TransferableOneWay vegetableMeal = CreateIngredientMeal(
                    map,
                    spot.Position,
                    "RawPotatoes",
                    10,
                    7,
                    180);

                ThingDef shelfDef = DefDatabase<ThingDef>.GetNamed("Shelf");
                Thing shelf = ThingMaker.MakeThing(shelfDef, ThingDefOf.Steel);
                MinifiedThing minifiedShelf = shelf.MakeMinified();
                Require(minifiedShelf != null, "the Shelf fixture did not minify");
                GenSpawn.Spawn(
                    minifiedShelf,
                    FindFixtureCell(map, spot.Position, 190),
                    map,
                    WipeMode.Vanish);
                var shelfRow = new TransferableOneWay();
                shelfRow.things.Add(minifiedShelf);
                shelfRow.AdjustTo(1);

                formation.transferables.Add(meatMeal);
                formation.transferables.Add(vegetableMeal);
                formation.transferables.Add(shelfRow);
                ReadinessSnapshotBuilder.Build(lord);

                formation.transferables.Remove(vegetableMeal);
                formation.transferables.Remove(meatMeal);
                formation.transferables.Insert(0, vegetableMeal);
                formation.transferables.Insert(1, meatMeal);
                ReadinessSnapshotBuilder.Build(lord);

                formation.transferables.Remove(vegetableMeal);
                VerifyTransferIdentitySnapshot(lord);
                Log.Message(LogPrefix +
                    "verificationTransferIdentity=staged duplicateDef=MealSimple" +
                    " survivorRequested=3 minifiedDisplay=Shelf");
            }
            catch (Exception exception)
            {
                Log.Error(LogPrefix +
                    "verificationTransferIdentity=fail " + exception);
            }
        }

        [DebugAction(
            "Caravan Readiness",
            "Verify transfer identity after load",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void VerifyTransferIdentityAfterLoad()
        {
            try
            {
                Lord lord = RequireFormation(RequireMap());
                VerifyTransferIdentitySnapshot(lord);
                VerifyTransferIdentitySnapshot(lord);
                Log.Message(LogPrefix +
                    "verificationTransferIdentity=load-complete" +
                    " duplicateDefHistory=preserved minifiedRefresh=preserved");
            }
            catch (Exception exception)
            {
                Log.Error(LogPrefix +
                    "verificationTransferIdentityLoad=fail " + exception);
            }
        }

        [DebugAction(
            "Caravan Readiness",
            "Add second real formation",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void AddSecondRealFormation()
        {
            try
            {
                Map map = RequireMap();
                Building spot = RequireSpot(map);
                Pawn pawn = map.mapPawns.FreeColonistsSpawned
                    .Where(candidate => candidate.GetLord()?.LordJob is not LordJob_FormAndSendCaravan)
                    .OrderBy(candidate => candidate.thingIDNumber)
                    .FirstOrDefault();
                Require(pawn != null, "no colonist is available for a second formation");
                TransferableOneWay component = CreateTransferable(
                    map,
                    spot.Position,
                    "ComponentIndustrial",
                    12,
                    9,
                    12);
                IntVec3 exitSpot;
                Require(
                    RCellFinder.TryFindClosestEdgeCellTo(spot.Position, map, out exitSpot),
                    "vanilla could not resolve a second exit spot");
                CaravanFormingUtility.StartFormingCaravan(
                    new List<Pawn> { pawn },
                    new List<Pawn>(),
                    Faction.OfPlayer,
                    new List<TransferableOneWay> { component },
                    spot.Position,
                    exitSpot,
                    map.Tile,
                    map.Tile);
                List<Lord> formations = FormationLocator.At(map, spot.Position);
                Require(formations.Count >= 2, "vanilla did not retain two formations");
                string ids = string.Join(",", formations.Select(item => item.loadID));
                string rows = string.Join(",", formations.Select(item =>
                    ((LordJob_FormAndSendCaravan)item.LordJob).transferables.Count));
                Log.Message(LogPrefix + "verificationMultiple=complete count=" +
                    formations.Count + " orderedIds=" + ids +
                    " separateManifestRows=" + rows);
            }
            catch (Exception exception)
            {
                Log.Error(LogPrefix + "verificationMultiple=fail " + exception);
            }
        }

        [DebugAction(
            "Caravan Readiness",
            "Run large manifest sample",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void RunLargeManifestSample()
        {
            try
            {
                Map map = RequireMap();
                Lord lord = RequireFormation(map);
                LordJob_FormAndSendCaravan formation =
                    (LordJob_FormAndSendCaravan)lord.LordJob;
                Building spot = RequireSpot(map);
                int added = 0;
                foreach (ThingDef definition in DefDatabase<ThingDef>
                    .AllDefsListForReading
                    .Where(definition =>
                        definition.category == ThingCategory.Item &&
                        definition.EverHaulable &&
                        definition.thingClass != null)
                    .OrderBy(definition => definition.defName))
                {
                    if (formation.transferables.Any(row => row.ThingDef == definition))
                    {
                        continue;
                    }
                    try
                    {
                        ThingDef stuff = definition.MadeFromStuff
                            ? GenStuff.DefaultStuffFor(definition)
                            : null;
                        Thing thing = ThingMaker.MakeThing(definition, stuff);
                        thing.stackCount = 1;
                        IntVec3 cell = FindFixtureCell(map, spot.Position, 14 + added);
                        GenSpawn.Spawn(thing, cell, map, WipeMode.Vanish);
                        TransferableOneWay row = new TransferableOneWay();
                        row.things.Add(thing);
                        row.AdjustTo(1);
                        formation.transferables.Add(row);
                        added++;
                    }
                    catch
                    {
                        // Some special item defs require bespoke generation. They are skipped.
                    }
                    if (added >= 60)
                    {
                        break;
                    }
                }
                Require(added >= 40, "fewer than forty real item definitions could be generated");
                Stopwatch stopwatch = Stopwatch.StartNew();
                FormationReadinessSnapshot snapshot = ReadinessSnapshotBuilder.Build(lord);
                stopwatch.Stop();
                Log.Message(LogPrefix + "verificationLargeManifest=complete rows=" +
                    snapshot.Cargo.Count + " problems=" + snapshot.Problems.Count +
                    " elapsedMs=" + stopwatch.Elapsed.TotalMilliseconds.ToString("F3"));
            }
            catch (Exception exception)
            {
                Log.Error(LogPrefix + "verificationLargeManifest=fail " + exception);
            }
        }

        [DebugAction(
            "Caravan Readiness",
            "Cancel verification formation",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void CancelVerificationFormation()
        {
            try
            {
                Map map = RequireMap();
                Lord lord = RequireFormation(map);
                int loadId = lord.loadID;
                CaravanFormingUtility.StopFormingCaravan(lord);
                Building spot = RequireSpot(map);
                int remaining = FormationLocator.At(map, spot.Position).Count;
                FormationBaselineComponent baselines =
                    map.GetComponent<FormationBaselineComponent>();
                bool gizmoAvailable = spot.TryGetComp<CompCaravanReadiness>()
                    .CompGetGizmosExtra()
                    .OfType<Command_Action>()
                    .Any();
                Log.Message(LogPrefix + "verificationCancel=complete removedLord=" +
                    loadId + " remainingAtSpot=" + remaining +
                    " baselineRecords=" + baselines.RecordCount +
                    " gizmoAvailable=" + gizmoAvailable);
            }
            catch (Exception exception)
            {
                Log.Error(LogPrefix + "verificationCancel=fail " + exception);
            }
        }

        [DebugAction(
            "Caravan Readiness",
            "Force verification departure",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ForceVerificationDeparture()
        {
            try
            {
                Lord lord = RequireFormation(RequireMap());
                int loadId = lord.loadID;
                CaravanFormingUtility.ForceCaravanDepart(lord);
                Log.Message(LogPrefix + "verificationDeparture=queued lord=" + loadId);
            }
            catch (Exception exception)
            {
                Log.Error(LogPrefix + "verificationDeparture=fail " + exception);
            }
        }

        [DebugAction(
            "Caravan Readiness",
            "Log verification snapshot",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void LogVerificationSnapshot()
        {
            try
            {
                Map map = RequireMap();
                Building spot = RequireSpot(map);
                List<Lord> formations = FormationLocator.At(
                    map,
                    spot.Position);
                if (formations.Count == 0)
                {
                    Log.Message(LogPrefix +
                        "verificationSnapshot=complete stage=manual " +
                        "noFormation=true baselineRecords=" +
                        map.GetComponent<FormationBaselineComponent>().RecordCount +
                        " gizmoAvailable=" +
                        spot.TryGetComp<CompCaravanReadiness>()
                            .CompGetGizmosExtra()
                            .OfType<Command_Action>()
                            .Any());
                    return;
                }
                LogSnapshot("manual", RequireFormation(map));
            }
            catch (Exception exception)
            {
                Log.Error(LogPrefix + "verificationSnapshot=fail " + exception);
            }
        }

        private static void LogSnapshot(string stage, Lord lord)
        {
            FormationReadinessSnapshot snapshot = ReadinessSnapshotBuilder.Build(lord);
            Require(snapshot != null, "snapshot was unavailable");
            string cargo = string.Join(";", snapshot.Cargo.Select(row =>
                row.Def.defName + "=" + row.Counts.Loaded + "/" +
                row.Counts.Requested + ",carried:" + row.Counts.Carried +
                ",reserved:" + row.Counts.Reserved +
                ",waiting:" + row.Counts.Waiting +
                ",unavailable:" + row.Counts.Unavailable +
                ",inaccessible:" + row.Counts.Inaccessible +
                ",blocked:" + row.Counts.Blocked +
                ",forbidden:" + row.HasForbidden +
                ",burning:" + row.HasBurning));
            Log.Message(LogPrefix + "verificationSnapshot=complete stage=" + stage +
                " lord=" + lord.loadID + " phase=\"" + snapshot.Phase +
                "\" cargoRows=" + snapshot.Cargo.Count +
                " people=" + snapshot.People.Count +
                " animals=" + snapshot.Animals.Count +
                " problems=" + snapshot.Problems.Count +
                " cargo=\"" + cargo + "\"");
        }

        private static Map RequireMap()
        {
            Map map = Find.CurrentMap;
            Require(map != null, "no current map");
            return map;
        }

        private static Building RequireSpot(Map map)
        {
            ThingDef definition = DefDatabase<ThingDef>.GetNamed("CaravanPackingSpot");
            Building spot = map.listerThings.ThingsOfDef(definition)
                .OfType<Building>()
                .FirstOrDefault(thing => thing.thingIDNumber == spotThingId)
                ?? map.listerThings.ThingsOfDef(definition)
                    .OfType<Building>()
                    .OrderBy(thing => thing.thingIDNumber)
                    .FirstOrDefault();
            Require(spot != null, "verification packing spot is unavailable");
            spotThingId = spot.thingIDNumber;
            return spot;
        }

        private static Building FindOrCreatePackingSpot(Map map)
        {
            ThingDef definition = DefDatabase<ThingDef>.GetNamed("CaravanPackingSpot");
            Building existing = map.listerThings.ThingsOfDef(definition)
                .OfType<Building>()
                .OrderBy(thing => thing.thingIDNumber)
                .FirstOrDefault();
            if (existing != null)
            {
                if (existing.Faction != Faction.OfPlayer)
                {
                    existing.SetFaction(Faction.OfPlayer);
                }
                return existing;
            }

            IntVec3 cell = FindFixtureCell(map, map.Center, 0);
            Building spot = (Building)ThingMaker.MakeThing(definition);
            spot.SetFaction(Faction.OfPlayer);
            return (Building)GenSpawn.Spawn(spot, cell, map, WipeMode.Vanish);
        }

        private static TransferableOneWay CreateTransferable(
            Map map,
            IntVec3 origin,
            string defName,
            int stackCount,
            int requested,
            int offset)
        {
            ThingDef definition = DefDatabase<ThingDef>.GetNamed(defName);
            Thing thing = ThingMaker.MakeThing(definition);
            thing.stackCount = Math.Min(stackCount, definition.stackLimit);
            IntVec3 cell = FindFixtureCell(map, origin, offset);
            GenSpawn.Spawn(thing, cell, map, WipeMode.Vanish);
            TransferableOneWay transferable = new TransferableOneWay();
            transferable.things.Add(thing);
            transferable.AdjustTo(Math.Min(requested, thing.stackCount));
            return transferable;
        }

        private static TransferableOneWay CreateIngredientMeal(
            Map map,
            IntVec3 origin,
            string ingredientDefName,
            int stackCount,
            int requested,
            int offset)
        {
            ThingDef mealDef = DefDatabase<ThingDef>.GetNamed("MealSimple");
            Thing meal = ThingMaker.MakeThing(mealDef);
            meal.stackCount = Math.Min(stackCount, mealDef.stackLimit);
            CompIngredients ingredients = meal.TryGetComp<CompIngredients>();
            Require(ingredients != null, "simple meal has no ingredient comp");
            ingredients.RegisterIngredient(
                DefDatabase<ThingDef>.GetNamed(ingredientDefName));
            GenSpawn.Spawn(
                meal,
                FindFixtureCell(map, origin, offset),
                map,
                WipeMode.Vanish);
            var transferable = new TransferableOneWay();
            transferable.things.Add(meal);
            transferable.AdjustTo(Math.Min(requested, meal.stackCount));
            return transferable;
        }

        private static void VerifyTransferIdentitySnapshot(Lord lord)
        {
            FormationReadinessSnapshot snapshot =
                ReadinessSnapshotBuilder.Build(lord);
            List<CargoReadinessRow> meals = snapshot.Cargo
                .Where(row => row.Def?.defName == "MealSimple")
                .ToList();
            Require(meals.Count == 1,
                "duplicate-def removal left the wrong number of meal rows");
            Require(meals[0].Counts.Requested == 3,
                "duplicate-def history moved to the wrong meal row");
            CargoReadinessRow shelf = snapshot.Cargo.FirstOrDefault(
                row => row.Def?.defName == "Shelf");
            Require(shelf != null,
                "minified Shelf display definition was not preserved");
            Require(shelf.Counts.Requested == 1,
                "minified Shelf request history reset during refresh/load");
        }

        private static IntVec3 FindFixtureCell(Map map, IntVec3 origin, int offset)
        {
            return GenRadial.RadialCellsAround(origin, 25f, true)
                .Where(cell => cell.InBounds(map) && cell.Standable(map))
                .OrderBy(cell => cell.DistanceToSquared(origin))
                .ThenBy(cell => cell.x)
                .ThenBy(cell => cell.z)
                .Skip(offset)
                .First();
        }

        private static Lord RequireFormation(Map map)
        {
            Building spot = RequireSpot(map);
            List<Lord> formations = FormationLocator.At(map, spot.Position);
            Lord lord = formations.FirstOrDefault(candidate =>
                candidate.loadID == preferredLordLoadId) ?? formations.FirstOrDefault();
            Require(lord != null, "no caravan is forming at the verification spot");
            preferredLordLoadId = lord.loadID;
            return lord;
        }

        private static TransferableOneWay RequiredRow(
            LordJob_FormAndSendCaravan formation,
            string defName)
        {
            TransferableOneWay row = formation.transferables.FirstOrDefault(
                candidate => candidate?.ThingDef?.defName == defName);
            Require(row != null, "manifest row is unavailable: " + defName);
            return row;
        }

        private static Thing RequiredSpawnedThing(
            TransferableOneWay row,
            string defName)
        {
            Thing thing = row.things.FirstOrDefault(candidate => candidate.Spawned);
            Require(
                thing != null,
                "verification row has no spawned source: " + defName);
            return thing;
        }

        private static CargoReadinessRow RequiredSnapshotRow(
            FormationReadinessSnapshot snapshot,
            string defName)
        {
            CargoReadinessRow row = snapshot?.Cargo.FirstOrDefault(
                candidate => candidate.Def?.defName == defName);
            Require(row != null, "snapshot row is unavailable: " + defName);
            return row;
        }

        private static void OpenSpotWindow(Building spot)
        {
            CompCaravanReadiness comp = spot.TryGetComp<CompCaravanReadiness>();
            Require(comp != null, "packing spot has no readiness component");
            Require(
                comp.CompGetGizmosExtra().OfType<Command_Action>().Any(),
                "the ordinary readiness gizmo is unavailable");
            comp.OpenReadiness();
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
