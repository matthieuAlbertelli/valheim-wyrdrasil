using Wyrdrasil.Core.Tool;
using Wyrdrasil.Settlements.Services;

namespace Wyrdrasil.Routines.Occupations;

public sealed class BedOccupationTargetSource : IOccupationTargetSource
{
    private readonly BedService _bedService;
    private readonly OccupationTargetFactory _targetFactory;

    public BedOccupationTargetSource(BedService bedService, OccupationTargetFactory targetFactory)
    {
        _bedService = bedService;
        _targetFactory = targetFactory;
    }

    public OccupationTargetKind TargetKind => OccupationTargetKind.Bed;

    public bool TryResolve(OccupationTargetRef targetRef, out OccupationTarget target)
    {
        if (targetRef.TargetKind != TargetKind ||
            !_bedService.TryGetBedById(targetRef.TargetId, out var bedData))
        {
            target = null!;
            return false;
        }

        var anchorDefinition = OccupationAnchorDefinition.FromExplicitPositions(
            OccupationAnchorAttachmentKind.Bed,
            bedData.ApproachPosition,
            bedData.SleepPosition,
            bedData.SleepForward,
            navigationStopDistance: 0.25f,
            engageRadius: 0.25f,
            sustainRadius: 0.90f,
            approachProfile: OccupationAnchorApproachProfile.BedDefault);

        target = _targetFactory.CreateAnchoredTarget(
            new OccupationTargetRef(TargetKind, bedData.Id),
            bedData.DisplayName,
            bedData.BuildingId,
            bedData.ZoneId,
            anchorDefinition,
            OccupationExecutionProfile.Bed(bedData.BedComponent, bedData.SleepAttachPoint, anchorDefinition));

        return true;
    }
}
