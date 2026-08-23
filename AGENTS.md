# Caravan Readiness Agent Guide

Own only this repository. Keep gameplay and UI local; use Spine only for its
runtime, patching, and opt-in tooltip-sizing contracts. This settings-free mod
must not acquire settings/contextual services.

Keep every mod-added gizmo at the far right with `Order = float.MaxValue` so
vanilla command placement is unchanged.

Use `About`, `Tools\CascadeManifest.json`, README, project
files, and `Tests` as the local build/support/verification authorities. Follow
`A:\Dev\RimWorld\AGENTS.md` for shared build and runtime work, then verify gizmo
ordering in game.
