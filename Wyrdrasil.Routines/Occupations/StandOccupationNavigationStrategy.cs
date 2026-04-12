using System.Collections.Generic;
using UnityEngine;
using Wyrdrasil.Routines.Services;

namespace Wyrdrasil.Routines.Occupations;

public sealed class StandOccupationNavigationStrategy : IOccupationNavigationStrategy
{
    public string StrategyId => OccupationExecutionProfile.StandStrategyId;

    public void NavigateAlongRoute(NpcNavigationService navigationService, Character character, IReadOnlyList<Vector3> routePoints, OccupationTarget target)
    {
        navigationService.NavigateAlongRouteToPosition(character, routePoints, target.Plan.EngagePosition, target.Plan.NavigationStopDistance, target.Plan.FacingDirection);
    }

    public void NavigateDirectly(NpcNavigationService navigationService, Character character, OccupationTarget target)
    {
        navigationService.NavigateDirectlyToPosition(character, target.Plan.EngagePosition, target.Plan.NavigationStopDistance, target.Plan.FacingDirection);
    }
}
