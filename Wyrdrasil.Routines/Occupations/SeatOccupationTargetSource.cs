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

        target = new OccupationTarget(
            new OccupationTargetRef(TargetKind, seatData.Id),
            seatData.DisplayName,
            seatData.BuildingId,
            seatData.ZoneId,
            new OccupationAnchor(seatData.ApproachPosition, seatData.SeatPosition, seatData.SeatForward),
            OccupationExecutionProfile.Seat(seatData.ChairComponent));

        return true;
    }
}
