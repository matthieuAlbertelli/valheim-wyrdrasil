using UnityEngine;

namespace Wyrdrasil.Routines.Occupations;

public sealed class OccupationExecutionProfile
{
    public const string StandStrategyId = "stand";
    public const string SeatStrategyId = "seat";
    public const string BedStrategyId = "bed";
    public const string CraftStationStrategyId = "craftstation";

    public string StrategyId { get; }
    public Chair? ChairComponent { get; }
    public Bed? BedComponent { get; }
    public Transform? AttachPoint { get; }
    public Interactable? Interactable { get; }

    private OccupationExecutionProfile(
        string strategyId,
        Chair? chairComponent = null,
        Bed? bedComponent = null,
        Transform? attachPoint = null,
        Interactable? interactable = null)
    {
        StrategyId = strategyId;
        ChairComponent = chairComponent;
        BedComponent = bedComponent;
        AttachPoint = attachPoint;
        Interactable = interactable;
    }

    public bool IsStand => StrategyId == StandStrategyId;
    public bool IsSeat => StrategyId == SeatStrategyId;
    public bool IsBed => StrategyId == BedStrategyId;
    public bool IsCraftStation => StrategyId == CraftStationStrategyId;

    public static OccupationExecutionProfile Stand()
    {
        return new OccupationExecutionProfile(StandStrategyId);
    }

    public static OccupationExecutionProfile Seat(Chair? chairComponent)
    {
        return new OccupationExecutionProfile(SeatStrategyId, chairComponent: chairComponent);
    }

    public static OccupationExecutionProfile Bed(Bed? bedComponent, Transform? attachPoint)
    {
        return new OccupationExecutionProfile(BedStrategyId, bedComponent: bedComponent, attachPoint: attachPoint);
    }

    public static OccupationExecutionProfile CraftStation(Interactable? interactable)
    {
        return new OccupationExecutionProfile(CraftStationStrategyId, interactable: interactable);
    }

    public static OccupationExecutionProfile Custom(
        string strategyId,
        Chair? chairComponent = null,
        Bed? bedComponent = null,
        Transform? attachPoint = null,
        Interactable? interactable = null)
    {
        return new OccupationExecutionProfile(strategyId, chairComponent, bedComponent, attachPoint, interactable);
    }
}
