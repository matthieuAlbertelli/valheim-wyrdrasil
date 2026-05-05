using Wyrdrasil.Core.Tool;
using Wyrdrasil.Settlements.Services;

namespace Wyrdrasil.Routines.Occupations;

public sealed class SeatOccupationTargetSource : IOccupationTargetSource
{
    private readonly SeatService _seatService;
    private readonly OccupationTargetFactory _targetFactory;

    public SeatOccupationTargetSource(SeatService seatService, OccupationTargetFactory targetFactory)
    {
        _seatService = seatService;
        _targetFactory = targetFactory;
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

        var anchorDefinition = OccupationAnchorDefinition.FromExplicitPositions(
            OccupationAnchorAttachmentKind.Seat,
            seatData.ApproachPosition,
            seatData.SeatPosition,
            seatData.SeatForward,
            navigationStopDistance: 0.25f,
            engageRadius: 0.25f,
            sustainRadius: 0.75f,
            approachProfile: OccupationAnchorApproachProfile.SeatDefault);

        target = _targetFactory.CreateAnchoredTarget(
            new OccupationTargetRef(TargetKind, seatData.Id),
            seatData.DisplayName,
            seatData.BuildingId,
            seatData.ZoneId,
            anchorDefinition,
            OccupationExecutionProfile.Seat(seatData.ChairComponent, anchorDefinition));

        return true;
    }
}
