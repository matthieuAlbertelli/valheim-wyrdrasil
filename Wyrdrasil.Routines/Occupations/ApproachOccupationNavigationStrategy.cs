using System.Collections.Generic;
using Wyrdrasil.Routines.Services;

namespace Wyrdrasil.Routines.Occupations;

public sealed class ApproachOccupationNavigationStrategy : IOccupationNavigationStrategy
{
    public string StrategyId => OccupationExecutionProfile.ApproachNavigationStrategyId;

    public void NavigateAlongRoute(NpcNavigationService navigationService, Character character, IReadOnlyList<UnityEngine.Vector3> routePoints, OccupationTarget target)
    {
        navigationService.NavigateAlongRouteToPosition(character, routePoints, target.Plan.ApproachPosition, target.Plan.NavigationStopDistance, target.Plan.FacingDirection);
    }

    public void NavigateDirectly(NpcNavigationService navigationService, Character character, OccupationTarget target)
    {
        navigationService.NavigateDirectlyToPosition(character, target.Plan.ApproachPosition, target.Plan.NavigationStopDistance, target.Plan.FacingDirection);
    }
}
