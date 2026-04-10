using Wyrdrasil.Routines.Occupations;
using Wyrdrasil.Souls.Services;
using Wyrdrasil.Souls.Tool;
using Wyrdrasil.Settlements.Services;

namespace Wyrdrasil.Routines.Services;

public sealed class OccupationExecutionService
{
    private readonly ResidentRuntimeService _runtimeService;
    private readonly NavigationWaypointService _waypointService;
    private readonly NpcNavigationService _navigationService;

    public OccupationExecutionService(
        ResidentRuntimeService runtimeService,
        NavigationWaypointService waypointService,
        NpcNavigationService navigationService)
    {
        _runtimeService = runtimeService;
        _waypointService = waypointService;
        _navigationService = navigationService;
    }

    public bool TryExecute(RegisteredNpcData resident, OccupationTarget target)
    {
        if (!_runtimeService.TryGetBoundCharacter(resident.Id, out var character))
        {
            return false;
        }

        if (_waypointService.TryBuildRoute(character.transform.position, target.Anchor.ApproachPosition, out var routePoints) && routePoints.Count > 0)
        {
            _navigationService.NavigateAlongRoute(character, routePoints, target);
            return true;
        }

        _navigationService.NavigateDirectly(character, target);
        return true;
    }
}
