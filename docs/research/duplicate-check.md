# Duplicate investigation

Investigated 2026-08-01 before gameplay implementation. Searches covered the
Steam Workshop, GitHub, Ludeon forums, r/RimWorld and r/RimWorldModding, the
local Workshop snapshot, and the supplied community/Discord archive. The
archive search found no released or proposed feature matching a live,
problem-oriented packing-readiness display.

## Closest maintained mods

- [Caravan Formation Improvements](https://steamcommunity.com/sharedfiles/filedetails/?id=2927335733)
  is current for RimWorld 1.6 (updated 2025-08-14). It changes formation,
  arrival, and exit spots and offers pause controls. It does not present a
  requested/carried/reserved/loaded/missing manifest or diagnose item/member
  blockers.
- [Easy Caravan and Go!](https://steamcommunity.com/sharedfiles/filedetails/?id=3490153225)
  is current for 1.6 (updated 2025-07-19). It adds fast formation, movable
  hitching/exit spots, and forced departure. It changes formation behavior;
  Caravan Readiness is observational and does not form, reroute, or force a
  caravan.
- [Caravan Lag Eliminator](https://steamcommunity.com/sharedfiles/filedetails/?id=2248500261)
  remains listed for 1.6 and changes the caravan-dialog food calculation. It
  does not inspect active packing.
- [Billy's Improved Caravan Formation](https://github.com/jopejope/BillysCaravanFormation)
  is an older job-behavior optimization. It is not a readiness interface.
- [Change Caravan Loadout](https://github.com/dhultgren/rimworld-change-caravan-loadout)
  allowed item removal during formation in older versions. RimWorld 1.6 now
  exposes removal in its forming-caravan tab; it does not diagnose readiness.

## Vanilla overlap

RimWorld 1.6 has `ITab_Pawn_FormingCaravan`. It shows a phase label, aggregate
member counts, remaining transferables, and all inventory on caravan pawns.
It does not show a stable requested total, in-flight or externally reserved
quantities, per-row availability/reachability, individual absent members, or
actionable blockers. It is attached only to caravan pawns, not the hitching
spot. Caravan Readiness enhances this existing process and does not introduce
a second staging mechanic.

## Decision

No maintained equivalent implements substantially the same player-facing
behavior. Proceed. The closest mods alter formation controls or job behavior;
this mod remains a read-only status and navigation layer.

No third-party source is copied. Third-party projects are used only for
behavioral and compatibility research.

