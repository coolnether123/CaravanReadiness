# Compatibility

## Investigated

- **Caravan Formation Improvements** is the closest maintained neighbor. It changes formation, exit and arrival spot handling, and pause behavior. Caravan Readiness remains observational and must be pair-tested before a compatibility claim.
- **Easy Caravan and Go!** changes formation convenience, movable spots, and forced departure. No compatibility claim has been made without a pair test.
- **Caravan Lag Eliminator** targets caravan-dialog food calculations rather than forming-lord readiness. No direct overlap was found.
- **Billy's Improved Caravan Formation** and **Change Caravan Loadout** are older job and loadout projects that were consulted only to define behavioral scope.
- Vanilla shelves and storage do not affect this mod. Vehicle Framework and transport pods use different formation state, so Caravan Readiness does not include them in vanilla caravan reports.

## Expected behavior

Modded `ThingDef` cargo is handled dynamically through vanilla transferables and `TransferAsOne(PodsOrCaravanPacking)`. No vanilla-only cargo list exists.

Multiple caravan-forming lords at one packing spot are shown separately, ordered by lord load ID. Their manifests are never summed or merged.

## Not claimed

- Vehicle Framework caravans
- transport-pod loading
- multiplayer synchronization
- DLC profiles other than the live Core/Biotech harness profile
- pairwise compatibility with the neighboring caravan mods above
