# Verification report

Date: 2026-08-01. RimWorld: 1.6.4871 rev574. The final shipping assembly hash is recorded in `Engineering/evidence.json` after the last clean build.

Portable provenance, raw-log hashes, active mods, and the exact release allowlist are recorded in [`Engineering/evidence.json`](../Engineering/evidence.json). Curated, path-free runtime lines are preserved under [`docs/evidence`](evidence/); the raw harness logs remain external and are identified by hash rather than copied with machine-specific paths.

## Automated and packaging checks

- Release/MSBuild build succeeded through the generalized RimWorld tooling against the registered RimWorld 1.6 references, Harmony 2.4.2, and Spine.
- The isolated suite passed 43 cargo-ledger, ordering, reservation-allocation, and structural-manifest assertions. Five manifest-observation assertions also passed for loading, manifest growth, manifest reduction, dropped inventory, and zero-clamping. No prior test was changed or weakened.
- New regressions prove a partial reservation leaves its remainder, two reservers on one stack do not overlap, over-reservation caps at stack size, transfer-group identities survive reorder, new rows receive new slots, removed rows are dropped, duplicate outer definitions retain the right history, and minified identities survive refresh/load.
- Harmony runtime audit: two patched methods, one prefix, one postfix, zero transpilers, zero finalizers, all owned by `CoolNether123.CaravanReadiness`.
- The final package validator returned `RWT-BUILD-PACKAGE-VALID` for the local release staging represented by the portable allowlist in `Engineering/evidence.json`.

## In-game scenarios completed

The generalized harness created controlled colonies and invoked DevMode-only fixtures that call vanilla `CaravanFormingUtility.StartFormingCaravan`. Production state remained a real `LordJob_FormAndSendCaravan`, real transferables, inventories, jobs, reservations, fire, walls, packing spot, and lord lifecycle.

- Initial five-row manifest: Cloth 10, industrial medicine 8, Silver 15, Steel 40, and Wood 20; two people and no animals.
- Carrying: a real `PrepareCaravan_GatherItems` job and carry tracker reported Steel `0 / 40`, carried 7, reserved 33.
- Loaded: transfer to a real pawn inventory reported Steel `7 / 40`, waiting 33.
- Live manifest edits: Steel increased to `7 / 43`, waiting 36; Wood decreased to `0 / 16`.
- Partial/multiple reservation regression: two unrelated pawns reserved 3 and 4 units from the same Wood stack. The snapshot reported blocked 7 and waiting 9, retaining all 16 requested units with no unavailable phantom remainder.
- Problems: burning Cloth, unavailable medicine 8, inaccessible Silver 15, and the split Wood reservation above.
- Save/reload during the active problem state retained all five cargo rows and the exact Wood blocked 7 / waiting 9 accounting.
- Structural manifest mutation removed Cloth, reordered retained rows, and added industrial components. The live snapshot preserved Steel 40 and Wood 20 baseline identities, assigned components a new requested total of 6, removed the stale Cloth row, and reported five correct rows.
- Transfer-group identity regression created two `MealSimple` rows that vanilla separates by ingredient/food grouping despite their shared outer `ThingDef`, initialized them to requested totals 3 and 7, reordered them, removed the 7-unit row, and retained the correct 3-unit history. A minified Steel Shelf remained displayed as `Shelf` with requested total 1 across repeated refresh and save/reload.
- Cancellation removed the vanilla lord and persisted baseline: `remainingAtSpot=0`, `baselineRecords=0`, readiness gizmo unavailable.
- Natural forced-ready departure completed under vanilla lord behavior and left `noFormation=true`, `baselineRecords=0`, readiness gizmo unavailable.
- Two simultaneous vanilla formations were kept separate and ordered deterministically by lord load ID; their manifest row counts were 5 and 1.
- A post-review 65-row synthetic large manifest produced a complete snapshot in 2.817 ms. Refresh remains limited to once per 120 ticks while the window is open. A many-dozen-pawn colony was not available in the controlled fixture, so extreme-colony scaling remains unverified.

## UI evidence

- [Rightmost gizmo](screenshots/gizmo-rightmost-final.png): Deconstruct, Form caravan, and Build copy remain in vanilla order; Caravan readiness is the far-right command.
- [Transfer identity after load](screenshots/transfer-identity-final.png): the surviving Simple meal remains `0 / 3`, the minified Steel shelf remains `0 / 1`, and the readiness command remains far right.
- [Narrow report](screenshots/narrow-window-final.png): the 720 × 480 minimum layout remains readable and uses the localized `Loaded / goal` heading without truncation.
- [Wide cargo view](screenshots/cargo-readiness-polished.png): full requested/loaded, carried, reserved, waiting, and problem columns.
- [Problems view](screenshots/problems-readiness-polished.png): concise blocking and warning rows with exact quantities.
- [Packing spot after save/reload](screenshots/packing-spot-after-reload.png): component and save/reload smoke evidence.

The harness' semantic action path could open every section and the production navigation method selected the expected target. Direct pointer injection into the Unity child window remained unavailable: the harness mouse bridge did not activate the control and the Windows capture API returned `0x80004002`. Therefore the exact physical row-click gesture is not claimed, although the same method called by that row completed successfully in game.

## Player.log analysis

The affected-workflow lane was `CaravanReadiness-2ec63e61f6f84074b771ff364c5524e5`; the final-DLL UI/order lane was `CaravanReadiness-d7251edb742e46d087e2bbe64c7bd52f`; and the final transfer-identity lane was `CaravanReadiness-c3a9a6e473aa4f16acbaa95c2ec69fb6`. Error and exception scans found no matching runtime entries from Caravan Readiness, RimWorld, Harmony, or Player.log.

Two startup warnings remain and are not caused by Caravan Readiness runtime behavior:

- RimWorld requests a public download URL for the local Spine dependency. No URL was fabricated for an unpublished local dependency.
- RimWorld Agent reports `ConnectedOutlineDrawer` may need `StaticConstructorOnStartup`; this type belongs to the generalized test harness.

All game processes stopped without forced termination and their lanes were released. The final identity lane then reported a harness-owned runtime-directory cleanup warning, `Access to the path 'RimWorldWin64.exe' is denied`; it occurred after process exit, did not affect the released status or evidence, and is recorded for harness hardening.

## Compatibility not claimed

Vehicle Framework, transport-pod workflows, Caravan Formation Improvements, and DLC combinations beyond Core plus Biotech were researched but not pair-tested. They remain explicitly unverified.
