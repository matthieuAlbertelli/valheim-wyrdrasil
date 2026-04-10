using Wyrdrasil.Core.Tool;
using Wyrdrasil.Settlements.Services;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Routines.Occupations;

public sealed class AssignedBedOccupationResolver : IOccupationResolver
{
    private readonly BedService _bedService;

    public AssignedBedOccupationResolver(BedService bedService)
    {
        _bedService = bedService;
    }

    public ResidentRoutineActivityType ActivityType => ResidentRoutineActivityType.SleepAtAssignedBed;

    public bool TryResolve(RegisteredNpcData resident, out OccupationTarget target)
    {
        if (!resident.TryGetAssignedTargetId(ResidentAssignmentPurpose.Sleep, OccupationTargetKind.Bed, out var bedId) ||
            !_bedService.TryGetBedById(bedId, out var bedData))
        {
            target = null!;
            return false;
        }

        target = new OccupationTarget(
            new OccupationTargetRef(OccupationTargetKind.Bed, bedData.Id),
            bedData.DisplayName,
            bedData.BuildingId,
            bedData.ZoneId,
            new OccupationAnchor(bedData.ApproachPosition, bedData.SleepPosition, bedData.SleepForward),
            OccupationUseMode.Lie,
            bedComponent: bedData.BedComponent,
            attachPoint: bedData.SleepAttachPoint);

        return true;
    }

    public void Release(RegisteredNpcData resident)
    {
    }
}
