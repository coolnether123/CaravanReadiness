using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI.Group;
using CaravanReadiness.State;

namespace CaravanReadiness.UI
{
    /// <summary>
    /// Attaches the observational readiness component to vanilla caravan
    /// packing spots without introducing a replacement building definition.
    /// </summary>
    public sealed class CompProperties_CaravanReadiness : CompProperties
    {
        public CompProperties_CaravanReadiness()
        {
            compClass = typeof(CompCaravanReadiness);
        }
    }

    /// <summary>
    /// Adds a rightmost readiness command only when the selected packing spot
    /// currently owns an active player caravan formation.
    /// </summary>
    public sealed class CompCaravanReadiness : ThingComp
    {
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (parent.Faction != Faction.OfPlayer || parent.Map == null)
            {
                yield break;
            }

            List<Lord> formations = FormationLocator.At(
                parent.Map,
                parent.Position);
            if (formations.Count == 0)
            {
                yield break;
            }

            yield return new Command_Action
            {
                defaultLabel = "CR_CommandLabel".Translate(),
                defaultDesc = "CR_CommandDescription".Translate(),
                icon = FormCaravanComp.FormCaravanCommand,
                Order = float.MaxValue,
                action = OpenReadiness
            };
        }

        public void OpenReadiness()
        {
            if (parent?.Map == null)
            {
                return;
            }

            List<Lord> formations = FormationLocator.At(
                parent.Map,
                parent.Position);
            if (formations.Count == 0)
            {
                return;
            }

            Find.WindowStack.Add(new Dialog_CaravanReadiness(
                parent.Map,
                parent.Position,
                formations[0].loadID));
        }
    }
}
