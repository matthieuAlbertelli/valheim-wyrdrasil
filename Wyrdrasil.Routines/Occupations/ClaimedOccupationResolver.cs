using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Routines.Occupations;

public sealed class ClaimedOccupationResolver : IOccupationResolver
{
    private readonly ResidentRoutineActivityType _activityType;
    private readonly OccupationClaimRegistry _claimRegistry;

    public ClaimedOccupationResolver(ResidentRoutineActivityType activityType, OccupationClaimRegistry claimRegistry)
    {
        _activityType = activityType;
        _claimRegistry = claimRegistry;
    }

    public ResidentRoutineActivityType ActivityType => _activityType;

    public bool TryResolve(RegisteredNpcData resident, out OccupationTarget target)
    {
        if (_claimRegistry.TryGetClaimSource(_activityType, out var claimSource) &&
            claimSource.TryClaim(resident, out target))
        {
            return true;
        }

        target = null!;
        return false;
    }

    public void Release(RegisteredNpcData resident)
    {
        if (_claimRegistry.TryGetClaimSource(_activityType, out var claimSource))
        {
            claimSource.Release(resident);
        }
    }
}
