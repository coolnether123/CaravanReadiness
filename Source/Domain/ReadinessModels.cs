using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace CaravanReadiness.Domain
{
    /// <summary>
    /// Identifies the report views independently of their translated labels
    /// so selection and filtering remain stable across languages.
    /// </summary>
    public enum ReadinessSection
    {
        Cargo,
        People,
        Animals,
        Problems
    }

    /// <summary>
    /// Distinguishes informational findings from warnings and conditions that
    /// prevent the caravan from being ready.
    /// </summary>
    public enum ReadinessSeverity
    {
        Information,
        Warning,
        Blocking
    }

    /// <summary>
    /// Carries one transfer group's reconciled state and optional navigation
    /// target from snapshot construction to the report UI.
    /// </summary>
    public sealed class CargoReadinessRow
    {
        public ThingDef Def;
        public string Label;
        public CargoCountLedger Counts;
        public Thing NavigationTarget;
        public bool HasForbidden;
        public bool HasBurning;
    }

    /// <summary>
    /// Presents a caravan member's readiness without exposing mutable pawn
    /// evaluation details to the drawing layer.
    /// </summary>
    public sealed class MemberReadinessRow
    {
        public Pawn Pawn;
        public string Status;
        public string Detail;
        public bool Ready;
        public bool IsBlocking;
    }

    /// <summary>
    /// Normalizes cargo and member impediments into one severity-aware problems
    /// view with optional world navigation.
    /// </summary>
    public sealed class ProblemReadinessRow
    {
        public string Label;
        public string Detail;
        public ReadinessSeverity Severity;
        public Thing NavigationTarget;
    }

    /// <summary>
    /// Freezes one formation's observed state for a UI refresh interval so
    /// repaint events never trigger repeated world scans.
    /// </summary>
    public sealed class FormationReadinessSnapshot
    {
        public Lord Lord;
        public int LordLoadId;
        public string DisplayName;
        public string Phase;
        public IntVec3 MeetingPoint;
        public List<CargoReadinessRow> Cargo = new List<CargoReadinessRow>();
        public List<MemberReadinessRow> People = new List<MemberReadinessRow>();
        public List<MemberReadinessRow> Animals = new List<MemberReadinessRow>();
        public List<ProblemReadinessRow> Problems = new List<ProblemReadinessRow>();
        public int RequestedTotal;
        public int LoadedTotal;
        public int CarriedTotal;
        public int ReservedTotal;
        public int MissingTotal;

        public bool IsStillActive =>
            Lord != null &&
            Lord.lordManager != null &&
            Lord.lordManager.lords.Contains(Lord) &&
            Lord.LordJob is LordJob_FormAndSendCaravan;
    }
}
