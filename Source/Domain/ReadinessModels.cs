using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace CaravanReadiness.Domain
{
    public enum ReadinessSection
    {
        Cargo,
        People,
        Animals,
        Problems
    }

    public enum ReadinessSeverity
    {
        Information,
        Warning,
        Blocking
    }

    public sealed class CargoReadinessRow
    {
        public ThingDef Def;
        public string Label;
        public CargoCountLedger Counts;
        public Thing NavigationTarget;
        public bool HasForbidden;
        public bool HasBurning;
    }

    public sealed class MemberReadinessRow
    {
        public Pawn Pawn;
        public string Status;
        public string Detail;
        public bool Ready;
        public bool IsBlocking;
    }

    public sealed class ProblemReadinessRow
    {
        public string Label;
        public string Detail;
        public ReadinessSeverity Severity;
        public Thing NavigationTarget;
    }

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
