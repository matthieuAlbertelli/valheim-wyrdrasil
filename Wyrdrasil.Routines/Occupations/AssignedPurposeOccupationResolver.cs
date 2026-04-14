using Wyrdrasil.Souls.Tool;
using Wyrdrasil.Core.Tool;

namespace Wyrdrasil.Routines.Occupations;

public sealed class AssignedPurposeOccupationResolver : IOccupationResolver
{
    private readonly ResidentRoutineActivityType _activityType;
    private readonly ResidentAssignmentPurpose _purpose;
    private readonly OccupationTargetCatalog _targetCatalog;

    public AssignedPurposeOccupationResolver(
        ResidentRoutineActivityType activityType,
        ResidentAssignmentPurpose purpose,
        OccupationTargetCatalog targetCatalog)
    {
        _activityType = activityType;
        _purpose = purpose;
        _targetCatalog = targetCatalog;
    }

    public ResidentRoutineActivityType ActivityType => _activityType;

    public bool TryResolve(RegisteredNpcData resident, out OccupationTarget target)
    {
        if (!resident.TryGetAssignedTarget(_purpose, out var targetRef))
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
