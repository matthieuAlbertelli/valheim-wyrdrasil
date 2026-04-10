using Wyrdrasil.Core.Tool;
using Wyrdrasil.Settlements.Services;

namespace Wyrdrasil.Routines.Occupations;

public sealed class BedOccupationTargetSource : IOccupationTargetSource
{
    private readonly BedService _bedService;

    public BedOccupationTargetSource(BedService bedService)
    {
        _bedService = bedService;
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

        target = new OccupationTarget(
            new OccupationTargetRef(TargetKind, bedData.Id),
            bedData.DisplayName,
            bedData.BuildingId,
            bedData.ZoneId,
            new OccupationAnchor(bedData.ApproachPosition, bedData.SleepPosition, bedData.SleepForward),
            OccupationExecutionProfile.Bed(bedData.BedComponent, bedData.SleepAttachPoint));

        return true;
    }
}
