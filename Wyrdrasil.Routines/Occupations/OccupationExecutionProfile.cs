using UnityEngine;

namespace Wyrdrasil.Routines.Occupations;

public sealed class OccupationExecutionProfile
{
    public const string StandStrategyId = "stand";
    public const string SeatStrategyId = "seat";
    public const string BedStrategyId = "bed";

    public string StrategyId { get; }
    public Chair? ChairComponent { get; }
    public Bed? BedComponent { get; }
    public Transform? AttachPoint { get; }

    private OccupationExecutionProfile(string strategyId, Chair? chairComponent = null, Bed? bedComponent = null, Transform? attachPoint = null)
    {
        StrategyId = strategyId;
        ChairComponent = chairComponent;
        BedComponent = bedComponent;
        AttachPoint = attachPoint;
    }

    public bool IsStand => StrategyId == StandStrategyId;
    public bool IsSeat => StrategyId == SeatStrategyId;
    public bool IsBed => StrategyId == BedStrategyId;

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

    public static OccupationExecutionProfile Custom(string strategyId, Chair? chairComponent = null, Bed? bedComponent = null, Transform? attachPoint = null)
    {
        return new OccupationExecutionProfile(strategyId, chairComponent, bedComponent, attachPoint);
    }
}
