using System.Collections.Generic;
using UnityEngine;
using Wyrdrasil.Routines.Services;

namespace Wyrdrasil.Routines.Occupations;

public sealed class BedOccupationNavigationStrategy : IOccupationNavigationStrategy
{
    public string StrategyId => OccupationExecutionProfile.BedStrategyId;

    public void NavigateAlongRoute(NpcNavigationService navigationService, Character character, IReadOnlyList<Vector3> routePoints, OccupationTarget target)
    {
        navigationService.NavigateAlongRouteToBed(character, routePoints, target);
    }

    public void NavigateDirectly(NpcNavigationService navigationService, Character character, OccupationTarget target)
    {
        navigationService.NavigateDirectlyToBed(character, target);
    }
}
