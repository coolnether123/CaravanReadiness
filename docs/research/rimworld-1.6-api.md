# RimWorld 1.6 caravan API investigation

Inspected build `1.6.4871 rev573` through the environment manifest and the
preserved ILSpy decompilation. `Assembly-CSharp.dll` file version is
`1.6.9676.17238`; SHA-256 is
`4A170804FBFEFABDB620D8914E584E58F822A58C6E304DCB76A67003588DAB28`.

## Authoritative state

- `CaravanFormingUtility.StartFormingCaravan` creates one
  `LordJob_FormAndSendCaravan` and copies only positive non-pawn
  `TransferableOneWay` rows into it.
- The lord owns active caravan members. `downedPawns` separately owns selected
  downed members. `meetingPoint` and `ExitSpot` are the active gathering and
  departure cells.
- `transferables[i].CountToTransfer` is the remaining quantity. Loading into a
  carrier decrements it. `transferables[i].things` retains the authoritative
  candidate references and receives split pieces while they are carried.
- A stable requested quantity can therefore be reconstructed as remaining plus
  matching things already in non-unloading caravan-member inventories. A
  carried/reserved quantity is a subset of remaining and must not be added to
  the requested total.
- `JobDriver_PrepareCaravan_GatherItems` exposes `ToHaul` and `Carrier`. Active
  gather jobs, their carry trackers, and their reservations identify in-flight
  work without creating or changing jobs.
- `ReservationManager.ReservationsReadOnly` exposes claimant, job, target, and
  stack count. A reservation by the same caravan gather job is “reserved for
  loading”; a respected reservation by unrelated work is “blocked”.
- `LordJob_FormAndSendCaravan.Status` and its current lord toil are the source
  for the overall phase. Vanilla checks completion every 120 ticks through
  `AllItemsLoadedOntoCaravan`.
- Pawn state (`Downed`, `InMentalState`, current job, reachability, rope state,
  and distance from `meetingPoint`/`ExitSpot`) supplies member and animal
  blockers. `MassUtility` supplies capacity state.
- `ThingDefOf.CaravanPackingSpot` is a normal building with
  `CompHitchingSpot`. Adding a comp by an XML patch gives a discoverable gizmo
  without patching vanilla selection or job code.

## Integration choice

The UI is a gizmo on the existing caravan hitching spot. It resolves every
forming-caravan lord whose current `meetingPoint` equals that spot. When a
formation uses a generated meeting cell rather than a placed spot, the vanilla
pawn tab remains available; this mod does not create a marker or staging
object. Multiple matching formations are shown in one selector rather than
silently choosing one.

Snapshots refresh at most every 120 game ticks while the window is open.
Enumeration is bounded to lord members, lord transferables and their referenced
things, active map reservations, and active gather jobs. There is no per-frame
full-map scan and no periodic map component.

## Compatibility risks

- Caravan Formation Improvements and Easy Caravan and Go can move the private
  `meetingPoint`. Reading its current value should follow those changes, but
  this is investigated, not yet runtime-verified compatibility.
- Pick Up And Haul touches hauling/inventory behavior. The status layer does
  not patch those methods and reports only vanilla caravan gather jobs as
  in-flight.
- Vehicle Framework replaces or patches several caravan APIs and uses vehicle
  caravan state. Vehicle formations are intentionally not claimed as
  supported; the vanilla-lord view should simply remain unavailable.
- Transport pods and shuttles use `CompTransporter.leftToLoad`, not
  `LordJob_FormAndSendCaravan`. They are outside the initial supported scope.
- Mods replacing `ThingDefOf.CaravanPackingSpot` or the lord job may not expose
  this UI. No compatibility is claimed without a runtime scenario.

