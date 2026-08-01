# Implementation plan and test matrix

## Plan

1. Add a comp to the vanilla hitching spot through an XML patch and expose one
   unobtrusive readiness gizmo only when a matching formation exists.
2. Build immutable snapshots from the active vanilla lord, transferables,
   inventories, gather jobs, reservations, reachability, and pawn state.
3. Render a cached, searchable window with Cargo, People, Animals, and Problems
   views; keep exact quantities and enable click-to-jump navigation.
4. Negotiate Spine runtime/tooltips, localize all player-facing text, and keep
   Harmony/job behavior untouched.
5. Test pure aggregation and classification, build/package, then exercise the
   controlled scenarios through `rwa.cmd` and inspect every session log.

## Test matrix

| Area | Scenarios | Expected evidence |
| --- | --- | --- |
| Cargo | small/large manifests, split stacks, carried, loaded, missing, forbidden, burning, inaccessible, unrelated reservations | Exact non-overlapping counts and actionable status |
| Members | absent pawn, downed pawn, mental break, pen animal, inaccessible animal | Individual problem rows and navigation |
| Lifecycle | change manifest, cancel/reform, simultaneous lords, save/reload, departure | Live reconciliation, no stale persisted state, no false remainder after departure |
| Definitions | modded item and animal defs | Definition-driven labels/icons with no vanilla-only list |
| Compatibility | Pick Up And Haul; Vehicle Framework observation | No patch collision; unsupported vehicle state is not misreported |
| Performance | large colony and large manifest, window open/closed | No per-frame map scan; cached refresh at 120 ticks; clean log |
| Packaging | clean build, tests, package validation, all mods together | Unique package/type IDs, resolved Spine dependency, no Harmony/log conflicts |

