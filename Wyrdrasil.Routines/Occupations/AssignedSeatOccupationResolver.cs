using Wyrdrasil.Settlements.Services;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Routines.Occupations;

public sealed class AssignedSeatOccupationResolver : IOccupationResolver
{
    private readonly SeatService _seatService;

    public AssignedSeatOccupationResolver(SeatService seatService)
    {
        _seatService = seatService;
    }

    public ResidentRoutineActivityType ActivityType => ResidentRoutineActivityType.SitAtAssignedSeat;

    public bool TryResolve(RegisteredNpcData resident, out OccupationTarget target)
    {
        if (!resident.AssignedSeatId.HasValue ||
            !_seatService.TryGetSeatById(resident.AssignedSeatId.Value, out var seatData))
        {
            target = null!;
            return false;
        }

        target = new OccupationTarget(
            new OccupationTargetRef(OccupationTargetKind.Seat, seatData.Id),
            seatData.DisplayName,
            seatData.BuildingId,
            seatData.ZoneId,
            new OccupationAnchor(seatData.ApproachPosition, seatData.SeatPosition, seatData.SeatForward),
            OccupationUseMode.Sit,
            chairComponent: seatData.ChairComponent);

        return true;
    }

    public void Release(RegisteredNpcData resident)
    {
    }
}
