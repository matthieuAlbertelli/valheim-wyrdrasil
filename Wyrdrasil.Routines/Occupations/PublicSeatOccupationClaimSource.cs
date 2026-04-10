using Wyrdrasil.Core.Tool;
using Wyrdrasil.Settlements.Services;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Routines.Occupations;

public sealed class PublicSeatOccupationClaimSource : IOccupationClaimSource
{
    private readonly SeatService _seatService;
    private readonly OccupationTargetCatalog _targetCatalog;

    public PublicSeatOccupationClaimSource(SeatService seatService, OccupationTargetCatalog targetCatalog)
    {
        _seatService = seatService;
        _targetCatalog = targetCatalog;
    }

    public ResidentRoutineActivityType ActivityType => ResidentRoutineActivityType.SitAtAvailablePublicSeat;

    public bool TryClaim(RegisteredNpcData resident, out OccupationTarget target)
    {
        if (!_seatService.TryReservePublicTavernSeat(resident.Id, out var seatData) || seatData == null)
        {
            target = null!;
            return false;
        }

        return _targetCatalog.TryResolve(new OccupationTargetRef(OccupationTargetKind.Seat, seatData.Id), out target);
    }

    public void Release(RegisteredNpcData resident)
    {
        _seatService.ReleasePublicSeatOccupation(resident.Id);
    }
}
