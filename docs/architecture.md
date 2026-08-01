# Architecture

`CompCaravanReadiness` is attached by XML to the vanilla `CaravanPackingSpot`. It exposes a gizmo only when one or more player caravan-forming lords use that exact meeting point.

`FormationLocator` reads the private meeting point through a cached Harmony field reference and returns matching lords ordered by stable `loadID`. `Dialog_CaravanReadiness` caches this list and a `FormationReadinessSnapshot`; it does not rescan during repaint. The snapshot refreshes at most once per 120 game ticks while the window is open.

`ReadinessSnapshotBuilder` derives cargo state from the lord's transferables, relevant inventories, active vanilla gather jobs, candidate-stack reservations, reachability from currently usable colonists, and current carrying capacity. A per-Thing classified-quantity ledger caps multiple reservations at the real stack size and passes any unreserved remainder to the accessibility classifier. It does not reserve targets or issue jobs. Member classification is similarly observational.

`FormationBaselineComponent` persists each row's transfer-group signature and representative separately from its display `ThingDef`, requested count, remaining count, and compatible inventory count. Before each snapshot, `ManifestSlotReconciler` atomically reorders all parallel lists by signature, while a representative comparison uses vanilla `TransferAsOne` semantics whenever the referenced thing remains available. The signature includes outer and minified-inner definitions plus relevant stuff, quality, health, ingredient, food-kind, gene, corpse, and pawn grouping dimensions. This prevents two transferables with the same outer `ThingDef` from exchanging history and keeps minified rows keyed by their outer group while displaying the inner building definition. New groups receive new slots and structurally removed groups are dropped. `ManifestRequestTracker` then observes quantity deltas so loading, changing manifest quantities, and dropping loaded cargo preserve an authoritative current-versus-requested total. Persisting the display definition also keeps a fully unavailable row visible after its final source is destroyed. Lifecycle hooks capture a baseline after vanilla starts formation and remove it before every vanilla lord-removal path; load-time reconciliation removes stale records.

The report clamps resizing to a screen-aware 720 × 480 design minimum (or the available screen when smaller) and uses a compact localized loaded/goal heading at that breakpoint. Its packing-spot command uses `Order = float.MaxValue`, which preserves vanilla ordering and places Caravan Readiness at the far right.

Harmony patches:

- Postfix `CaravanFormingUtility.StartFormingCaravan` to capture the initial manifest.
- Prefix `LordManager.RemoveLord` with a narrow `LordJob_FormAndSendCaravan` guard to clean state for cancellation, departure, and every vanilla lord-removal path while the map reference is still authoritative.

There are no transpilers, job changes, reservation changes, or full-map scans per frame.
