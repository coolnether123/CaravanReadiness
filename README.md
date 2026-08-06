# Caravan Readiness

Caravan Readiness adds an observational readiness report to RimWorld's existing
caravan hitching spot. It does not replace caravan formation, create haul jobs,
change reservations, or force departure.

When a vanilla caravan is forming, select its hitching spot and open **Caravan
readiness**. A compact summary and progress bar stay readable as the caravan
fills, while the detailed report groups cargo, people, animals, and problems.
Cargo rows reconcile requested, loaded, carried, reserved, waiting,
inaccessible, blocked, and unavailable quantities. Rows with a known target can
be clicked to jump to the relevant pawn or item.

## Requirements

- RimWorld 1.6
- [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077)
- [SpineLib](https://github.com/coolnether123/Spine) — the shared runtime used by
  CoolNether123 mods

## Installation

Install Harmony and SpineLib, copy `CaravanReadiness` into RimWorld's `Mods`
folder, then enable Harmony, SpineLib, and Caravan Readiness in that order.

Caravan Readiness adds no save-persistent world objects, jobs, or replacement
staging system, so it is safe to add to or remove from an existing save.

## Design boundaries

- Uses the vanilla `LordJob_FormAndSendCaravan`, transferables, inventories,
  gather jobs, reservations, and meeting point as authoritative state.
- Refreshes every 120 game ticks only while the report window is open.
- Resolves simultaneous formations deterministically by lord load ID and never
  merges manifests.
- Reconciles baselines by the vanilla transfer group's stable signature rather
  than display `ThingDef`, so quality/stuff variants and minified buildings
  cannot inherit another row's history after add, remove, reorder, refresh, or
  load.
- Accounts for each reservation's bounded quantity; partial and multiple
  reservations leave the unreserved stack remainder available for waiting or
  reachability classification.
- Adds one start-formation postfix and one cleanup prefix; no transpilers.
- Places its command after vanilla gizmos and clamps the resizable report to a
  safe, screen-aware minimum with a compact cargo heading at narrow width.

## Known limitations

- The controlled live session exercised real vanilla formations, cargo jobs,
  reservations, problems, save/reload, cancellation, departure, simultaneous
  formations, and the detailed interface. Direct physical pointer injection
  into the Unity child window was unavailable, so the exact row-click gesture
  remains unverified; the same production navigation method selected the
  correct live target through the semantic test action.
- Vehicle Framework and transport-pod workflows are investigated but not
  claimed compatible.
- The current test depot exposed Core and Biotech; other DLC combinations
  remain unverified.
- The post-review 65-row manifest sample completed in 2.817 ms on a small
  controlled colony. Extreme many-dozen-pawn colony scaling has not been
  measured.

## Documentation

- [Duplicate research](docs/research/duplicate-check.md)
- [Architecture](docs/architecture.md)
- [Compatibility](docs/compatibility.md)
- [Verification record](docs/verification.md)

## Developer fixture

Live debug actions are isolated in `Developer/CaravanReadiness.TestFixture`, a
separately loadable developer mod. Build and load that folder only for harness
verification; it is never part of the Caravan Readiness shipping package.

## License

Released under the [MIT License](LICENSE). Harmony and SpineLib are used under
their own licenses. Existing caravan mods were consulted only for behavioral
and compatibility research; no code was copied.
