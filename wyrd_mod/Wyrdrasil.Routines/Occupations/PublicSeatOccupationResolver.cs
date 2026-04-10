using Wyrdrasil.Settlements.Services;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Routines.Occupations;

public sealed class PublicSeatOccupationResolver : IOccupationResolver
{
    private readonly SeatService _seatService;

    public PublicSeatOccupationResolver(SeatService seatService)
    {
        _seatService = seatService;
    }

    public ResidentRoutineActivityType ActivityType => ResidentRoutineActivityType.SitAtAvailablePublicSeat;

    public bool TryResolve(RegisteredNpcData resident, out OccupationTarget target)
    {
        if (!_seatService.TryReservePublicTavernSeat(resident.Id, out var seatData) || seatData == null)
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
        _seatService.ReleasePublicSeatOccupation(resident.Id);
    }
}
