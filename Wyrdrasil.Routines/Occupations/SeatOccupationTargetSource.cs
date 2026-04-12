using Wyrdrasil.Core.Tool;
using Wyrdrasil.Settlements.Services;

namespace Wyrdrasil.Routines.Occupations;

public sealed class SeatOccupationTargetSource : IOccupationTargetSource
{
    private readonly SeatService _seatService;

    public SeatOccupationTargetSource(SeatService seatService)
    {
        _seatService = seatService;
    }

    public OccupationTargetKind TargetKind => OccupationTargetKind.Seat;

    public bool TryResolve(OccupationTargetRef targetRef, out OccupationTarget target)
    {
        if (targetRef.TargetKind != TargetKind ||
            !_seatService.TryGetSeatById(targetRef.TargetId, out var seatData))
        {
            target = null!;
            return false;
        }

        var plan = new OccupationPosePlan(
            seatData.ApproachPosition,
            seatData.SeatPosition,
            seatData.SeatForward,
            0.25f,
            0.25f,
            0.75f);

        target = new OccupationTarget(
            new OccupationTargetRef(TargetKind, seatData.Id),
            seatData.DisplayName,
            seatData.BuildingId,
            seatData.ZoneId,
            plan,
            OccupationExecutionProfile.Seat(seatData.ChairComponent));

        return true;
    }
}
