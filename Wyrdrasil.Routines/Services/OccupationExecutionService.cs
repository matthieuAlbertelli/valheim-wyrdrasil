using Wyrdrasil.Routines.Occupations;
using Wyrdrasil.Settlements.Services;
using Wyrdrasil.Souls.Components;
using Wyrdrasil.Souls.Services;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Routines.Services;

public sealed class OccupationExecutionService
{
    private readonly ResidentRuntimeService _runtimeService;
    private readonly NavigationWaypointService _waypointService;
    private readonly NpcNavigationService _navigationService;
    private readonly OccupationLifecycleStrategyRegistry _lifecycleStrategyRegistry;

    public OccupationExecutionService(
        ResidentRuntimeService runtimeService,
        NavigationWaypointService waypointService,
        NpcNavigationService navigationService,
        OccupationLifecycleStrategyRegistry lifecycleStrategyRegistry)
    {
        _runtimeService = runtimeService;
        _waypointService = waypointService;
        _navigationService = navigationService;
        _lifecycleStrategyRegistry = lifecycleStrategyRegistry;
    }

    public bool TryBeginExecution(RegisteredNpcData resident, OccupationTarget target, out OccupationPhase phase)
    {
        phase = OccupationPhase.None;

        if (!_runtimeService.TryGetBoundCharacter(resident.Id, out var character) ||
            !_lifecycleStrategyRegistry.TryGetStrategy(target.Execution.StrategyId, out var strategy))
        {
            return false;
        }

        phase = strategy.Begin(this, resident, character, target);
        return phase != OccupationPhase.None;
    }

    public bool TryContinueExecution(RegisteredNpcData resident, OccupationTarget target, OccupationPhase currentPhase, out OccupationPhase nextPhase)
    {
        nextPhase = OccupationPhase.None;

        if (!_runtimeService.TryGetBoundCharacter(resident.Id, out var character) ||
            !_lifecycleStrategyRegistry.TryGetStrategy(target.Execution.StrategyId, out var strategy))
        {
            return false;
        }

        nextPhase = strategy.Continue(this, resident, character, target, currentPhase);
        return true;
    }

    public void ReleaseExecution(RegisteredNpcData resident, OccupationTarget target, bool detachIfAttached = true)
    {
        if (!_runtimeService.TryGetBoundCharacter(resident.Id, out var character))
        {
            return;
        }

        if (_lifecycleStrategyRegistry.TryGetStrategy(target.Execution.StrategyId, out var strategy))
        {
            strategy.Release(this, resident, character, target);
        }

        _navigationService.ReleaseOccupation(character, detachIfAttached);
    }

    public void ReleaseResidentNavigation(RegisteredNpcData resident, bool detachIfAttached = true)
    {
        if (_runtimeService.TryGetBoundCharacter(resident.Id, out var character))
        {
            _navigationService.ReleaseOccupation(character, detachIfAttached);
        }
    }

    public bool TryApproachTarget(Character character, OccupationTarget target)
    {
        if (_waypointService.TryBuildRoute(character.transform.position, target.Anchor.ApproachPosition, out var routePoints) && routePoints.Count > 0)
        {
            _navigationService.NavigateAlongRoute(character, routePoints, target);
            return true;
        }

        _navigationService.NavigateDirectly(character, target);
        return true;
    }

    public bool IsNavigationActive(Character character)
    {
        return _navigationService.IsNavigationActive(character);
    }

    public bool IsOccupyingTarget(Character character, OccupationTarget target)
    {
        if (target.Execution.IsStand)
        {
            return !_navigationService.IsNavigationActive(character);
        }

        if (target.Execution.IsSeat)
        {
            if (character is WyrdrasilVikingNpc seatViking && target.Execution.ChairComponent != null)
            {
                return seatViking.IsAttachedToChair(target.Execution.ChairComponent);
            }

            return character is Humanoid seatHumanoid && seatHumanoid.IsAttached();
        }

        if (target.Execution.IsBed)
        {
            if (character is WyrdrasilVikingNpc bedViking && target.Execution.BedComponent != null)
            {
                return bedViking.IsAttachedToBed(target.Execution.BedComponent);
            }

            return character is Humanoid bedHumanoid && bedHumanoid.IsAttached();
        }

        return false;
    }
}
