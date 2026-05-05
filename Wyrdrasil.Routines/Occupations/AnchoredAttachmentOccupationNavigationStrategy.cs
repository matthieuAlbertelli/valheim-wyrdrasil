using System.Collections.Generic;
using UnityEngine;
using Wyrdrasil.Routines.Services;

namespace Wyrdrasil.Routines.Occupations;

/// <summary>
/// Shared navigation strategy for occupations that must walk to an approach point and then
/// attach to a concrete anchor, such as a chair or bed.
/// </summary>
public sealed class AnchoredAttachmentOccupationNavigationStrategy : IOccupationNavigationStrategy
{
    public AnchoredAttachmentOccupationNavigationStrategy(string strategyId)
    {
        StrategyId = strategyId;
    }

    public string StrategyId { get; }

    public void NavigateAlongRoute(NpcNavigationService navigationService, Character character, IReadOnlyList<Vector3> routePoints, OccupationTarget target)
    {
        navigationService.NavigateAlongRouteToAnchor(character, routePoints, target);
    }

    public void NavigateDirectly(NpcNavigationService navigationService, Character character, OccupationTarget target)
    {
        navigationService.NavigateDirectlyToAnchor(character, target);
    }
}
