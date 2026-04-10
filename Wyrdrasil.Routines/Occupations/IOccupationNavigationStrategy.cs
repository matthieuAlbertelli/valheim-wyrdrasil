using System.Collections.Generic;
using UnityEngine;
using Wyrdrasil.Routines.Services;

namespace Wyrdrasil.Routines.Occupations;

public interface IOccupationNavigationStrategy
{
    string StrategyId { get; }

    void NavigateAlongRoute(NpcNavigationService navigationService, Character character, IReadOnlyList<Vector3> routePoints, OccupationTarget target);

    void NavigateDirectly(NpcNavigationService navigationService, Character character, OccupationTarget target);
}
