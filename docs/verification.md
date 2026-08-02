# Verification report

Date: 2026-08-01. RimWorld: 1.6.4871 rev574. The exact redesigned assembly hash and final live lane are recorded below; `Engineering/evidence.json` will be regenerated with the complete suite during release packaging.

Portable provenance, raw-log hashes, active mods, and the exact release allowlist are recorded in [`Engineering/evidence.json`](../Engineering/evidence.json). Curated, path-free runtime lines are preserved under [`docs/evidence`](evidence/); the raw harness logs remain external and are identified by hash rather than copied with machine-specific paths.

## Current release candidate hardening — 2026-08-02

- The centralized-service candidate rebuild completed with zero warnings and
  zero errors against Spine SHA-256
  `3E857A09793BBFF839D0C18D197E480C9365B6384148F49F48669F068BBB9086`.
- The current `CaravanReadiness.dll` is 52,224 bytes, has assembly version
  1.0.0.0, and has SHA-256
  `912B5E0A302073F489D0627E9789D92D5C872AB499A50DC61774AEB2040AB727`.
- The focused suite passes 25 contracts and 81 assertions, and
  `Test-RwtPackage` returns `RWT-BUILD-PACKAGE-VALID`.
- The shipping package has one DLL and excludes
  `Developer/CaravanReadiness.TestFixture`; fixture source and metadata remain
  available to developers. Runtime records below remain bound to their exact
  historical hashes, so the parent release pass must record the final combined
  launch for this candidate.

## Progress-label contrast correction — 2026-08-02

- The loaded/goal label is now rendered after the fill with a one-pixel four-sided dark outline and a two-pixel protected inset. Its glyphs remain distinguishable when the moving fill edge passes directly through the text.
- Pure render-contract coverage exercises progress at empty, near-empty, 50%, near-full, full, and out-of-range values. It also proves the outline remains within a normal 720 × 20 bar and that tiny geometry never produces negative bounds.
- The focused suite now reports `PASS: 81 Caravan Readiness assertions`; no prior expectation was changed.
- The progress-label runtime assembly had SHA-256
  `6B6E0765DCDBA40689006A14A5B45333FF83BD077ACB3BF2A2355A66EEDBA02F`.

## Loaded-cargo accounting correction — 2026-08-02

- The release-blocker reproduction was traced to `CargoInventoryCounter`: it treated `Pawn_InventoryTracker.UnloadEverything` as though it proved every item in the pawn inventory was unrelated to the active caravan. Vanilla uses that flag as delayed cancellation cleanup, and it can still be set when the next formation transfers a selected stack into the same pawn inventory.
- The counter now keeps the transferable group's existing `TransferAsOne` membership check and no longer discards that whole inventory. This preserves the authoritative `40 requested / 7 loaded / 33 remaining` Steel transition after the carry tracker transfers the stack into a caravan member inventory.
- Focused automated verification passed: `dotnet run --project Tests\\Mod.Tests.csproj -c Release` reported `PASS: 65 Caravan Readiness assertions`, including the new carry-to-inventory regression. The unchanged debug action `Stage loaded cargo` remains the live assertion for the exact production transition.
- The cargo correction's intermediate 1.6 assembly was built by the shared RimWorld tooling with resolved Harmony and Spine dependencies. Its superseded DLL SHA-256 was `E55BCEA1F8027D5E9DF4E039879DE009E9EBAC133FE915C3463A5DC79CE18A2E`; the current candidate hash is recorded above.
- Live rerun is pending the concurrent harness mailbox repair. Two isolated lanes reached Entry and then their harness-owned startup commands remained queued; this is recorded as a harness boundary, not a Caravan Readiness pass.

## Automated and packaging checks

- Release/MSBuild build succeeded through the generalized RimWorld tooling against the registered RimWorld 1.6 references, Harmony 2.4.2, and Spine.
- The isolated suite passed 62 cargo-ledger, ordering, reservation-allocation, structural-manifest, adaptive-height, and responsive-column assertions. Five manifest-observation assertions also passed for loading, manifest growth, manifest reduction, dropped inventory, and zero-clamping. No prior test was changed or weakened.
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

- [Compact readiness view](screenshots/readiness-window-final-compact.png): the exact final DLL shrinks an all-clear Problems view to its content instead of leaving a large empty fixed-height window.
- [Cargo view](screenshots/cargo-window-final.png): the exact final DLL shows the full Loaded, Carried, Reserved, Waiting, and Problems headings with alternating native rows and item icons.
- [Narrow cargo view](screenshots/narrow-window-final-adaptive.png): at the 560 px width floor, the least useful diagnostic column collapses so every remaining heading and quantity stays legible.

- [Rightmost gizmo](screenshots/gizmo-rightmost-final.png): Deconstruct, Form caravan, and Build copy remain in vanilla order; Caravan readiness is the far-right command.
- [Transfer identity after load](screenshots/transfer-identity-final.png): the surviving Simple meal remains `0 / 3`, the minified Steel shelf remains `0 / 1`, and the readiness command remains far right.
- [Problems view](screenshots/problems-readiness-polished.png): concise blocking and warning rows with exact quantities.
- [Packing spot after save/reload](screenshots/packing-spot-after-reload.png): component and save/reload smoke evidence.

The harness opened every section, used real Unity-client pointer clicks to switch tabs, and the production navigation method selected the expected target.

## Player.log analysis

The exact redesigned DLL (`9DD224D416B06554EF151E2D0E4861C20761B4EAB16E9A69D8C5C367169B33A2`) was verified in lane `new-four-0cfd0dbeb3e447baa914191239b90bab` with all four new gameplay mods and Spine enabled. The compact, full-width cargo, and narrow cargo views were exercised through real Unity pointer input. The in-game log contained no errors or exceptions; only the already documented missing public download-URL warnings for the unpublished local Spine dependency remained. Earlier affected-workflow, UI/order, and transfer-identity lanes remain `CaravanReadiness-2ec63e61f6f84074b771ff364c5524e5`, `CaravanReadiness-d7251edb742e46d087e2bbe64c7bd52f`, and `CaravanReadiness-c3a9a6e473aa4f16acbaa95c2ec69fb6`.

Two startup warnings remain and are not caused by Caravan Readiness runtime behavior:

- RimWorld requests a public download URL for the local Spine dependency. No URL was fabricated for an unpublished local dependency.
- The earlier `ConnectedOutlineDrawer` startup warning came from Spine, not the generalized test harness. Spine now marks that shared drawer with `StaticConstructorOnStartup`; the final combined run must confirm the warning is gone.

All game processes stopped without forced termination and their lanes were released. The final identity lane then reported a harness-owned runtime-directory cleanup warning, `Access to the path 'RimWorldWin64.exe' is denied`; it occurred after process exit, did not affect the released status or evidence, and is recorded for harness hardening.

## Compatibility not claimed

Vehicle Framework, transport-pod workflows, Caravan Formation Improvements, and DLC combinations beyond Core plus Biotech were researched but not pair-tested. They remain explicitly unverified.
