using UnityEngine;

namespace Wyrdrasil.Routines.Occupations;

public sealed class OccupationExecutionProfile
{
    public enum OccupationExecutionKind
    {
        Stand,
        Seat,
        Bed,
        CraftStation
    }

    public const string StandStrategyId = "stand";
    public const string SeatStrategyId = "seat";
    public const string BedStrategyId = "bed";
    public const string ApproachNavigationStrategyId = "approach";
    public const string AnchoredStandLifecycleStrategyId = "anchored-stand.lifecycle";
    public const string WorkbenchLifecycleStrategyId = "workbench.lifecycle";
    public const string CraftStationLifecycleStrategyId = WorkbenchLifecycleStrategyId;
    public const string CraftStationSustainStrategyId = "craftstation.sustain";
    public const string ConstructionWorkSustainStrategyId = "construction.work";

    public string NavigationStrategyId { get; }
    public string LifecycleStrategyId { get; }
    public string SustainStrategyId { get; }
    public OccupationExecutionKind Kind { get; }
    public Chair? ChairComponent { get; }
    public Bed? BedComponent { get; }
    public Transform? AttachPoint { get; }
    public Interactable? Interactable { get; }

    private OccupationExecutionProfile(
        string navigationStrategyId,
        string lifecycleStrategyId,
        string sustainStrategyId,
        OccupationExecutionKind kind,
        Chair? chairComponent = null,
        Bed? bedComponent = null,
        Transform? attachPoint = null,
        Interactable? interactable = null)
    {
        NavigationStrategyId = navigationStrategyId;
        LifecycleStrategyId = lifecycleStrategyId;
        SustainStrategyId = sustainStrategyId;
        Kind = kind;
        ChairComponent = chairComponent;
        BedComponent = bedComponent;
        AttachPoint = attachPoint;
        Interactable = interactable;
    }

    public bool IsStand => Kind == OccupationExecutionKind.Stand;
    public bool IsSeat => Kind == OccupationExecutionKind.Seat;
    public bool IsBed => Kind == OccupationExecutionKind.Bed;
    public bool IsCraftStation => Kind == OccupationExecutionKind.CraftStation;

    public static OccupationExecutionProfile Stand()
    {
        return new OccupationExecutionProfile(StandStrategyId, StandStrategyId, StandStrategyId, OccupationExecutionKind.Stand);
    }

    public static OccupationExecutionProfile AnchoredStand(string sustainStrategyId)
    {
        return new OccupationExecutionProfile(ApproachNavigationStrategyId, AnchoredStandLifecycleStrategyId, sustainStrategyId, OccupationExecutionKind.Stand);
    }

    public static OccupationExecutionProfile Seat(Chair? chairComponent)
    {
        return new OccupationExecutionProfile(SeatStrategyId, SeatStrategyId, SeatStrategyId, OccupationExecutionKind.Seat, chairComponent: chairComponent);
    }

    public static OccupationExecutionProfile Bed(Bed? bedComponent, Transform? attachPoint)
    {
        return new OccupationExecutionProfile(BedStrategyId, BedStrategyId, BedStrategyId, OccupationExecutionKind.Bed, bedComponent: bedComponent, attachPoint: attachPoint);
    }

    public static OccupationExecutionProfile Workbench(string sustainStrategyId, Interactable? interactable)
    {
        return new OccupationExecutionProfile(
            ApproachNavigationStrategyId,
            WorkbenchLifecycleStrategyId,
            sustainStrategyId,
            OccupationExecutionKind.CraftStation,
            interactable: interactable);
    }

    public static OccupationExecutionProfile CraftStation(Interactable? interactable)
    {
        return Workbench(CraftStationSustainStrategyId, interactable);
    }

    public static OccupationExecutionProfile ConstructionWork(Interactable? interactable)
    {
        return Workbench(ConstructionWorkSustainStrategyId, interactable);
    }
}
