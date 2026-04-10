using Wyrdrasil.Core.Tool;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Routines.Occupations;

public sealed class AssignedOccupationResolver : IOccupationResolver
{
    private readonly ResidentRoutineActivityType _activityType;
    private readonly ResidentAssignmentPurpose _purpose;
    private readonly OccupationTargetKind _expectedTargetKind;
    private readonly OccupationTargetCatalog _targetCatalog;

    public AssignedOccupationResolver(
        ResidentRoutineActivityType activityType,
        ResidentAssignmentPurpose purpose,
        OccupationTargetKind expectedTargetKind,
        OccupationTargetCatalog targetCatalog)
    {
        _activityType = activityType;
        _purpose = purpose;
        _expectedTargetKind = expectedTargetKind;
        _targetCatalog = targetCatalog;
    }

    public ResidentRoutineActivityType ActivityType => _activityType;

    public bool TryResolve(RegisteredNpcData resident, out OccupationTarget target)
    {
        if (!resident.TryGetAssignedTarget(_purpose, out var targetRef) || targetRef.TargetKind != _expectedTargetKind)
        {
            target = null!;
            return false;
        }

        return _targetCatalog.TryResolve(targetRef, out target);
    }

    public void Release(RegisteredNpcData resident)
    {
    }
}
