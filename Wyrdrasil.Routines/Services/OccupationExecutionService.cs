using UnityEngine;
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
    private readonly OccupationSustainStrategyRegistry _sustainStrategyRegistry;

    public OccupationExecutionService(
        ResidentRuntimeService runtimeService,
        NavigationWaypointService waypointService,
        NpcNavigationService navigationService,
        OccupationLifecycleStrategyRegistry lifecycleStrategyRegistry,
        OccupationSustainStrategyRegistry sustainStrategyRegistry)
    {
        _runtimeService = runtimeService;
        _waypointService = waypointService;
        _navigationService = navigationService;
        _lifecycleStrategyRegistry = lifecycleStrategyRegistry;
        _sustainStrategyRegistry = sustainStrategyRegistry;
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

    public bool TryContinueExecution(RegisteredNpcData resident, OccupationSession session, out OccupationPhase nextPhase)
    {
        nextPhase = OccupationPhase.None;
        var target = session.Target;

        if (!_runtimeService.TryGetBoundCharacter(resident.Id, out var character) ||
            !_lifecycleStrategyRegistry.TryGetStrategy(target.Execution.StrategyId, out var lifecycleStrategy))
        {
            return false;
        }

        if (session.Phase == OccupationPhase.Sustain &&
            _sustainStrategyRegistry.TryGetStrategy(target.Execution.StrategyId, out var sustainStrategy))
        {
            var now = Time.time;
            if (!session.ShouldRunSustainTick(sustainStrategy.TickIntervalSeconds, now))
            {
                nextPhase = OccupationPhase.Sustain;
                return true;
            }

            switch (sustainStrategy.Sustain(this, resident, character, target, session))
            {
                case OccupationSustainResult.Continue:
                    session.RegisterSustainTick(now);
                    nextPhase = OccupationPhase.Sustain;
                    return true;

                case OccupationSustainResult.Complete:
                    session.RegisterSustainTick(now);
                    nextPhase = OccupationPhase.None;
                    return true;

                case OccupationSustainResult.Abort:
                default:
                    nextPhase = OccupationPhase.None;
                    return true;
            }
        }

        nextPhase = lifecycleStrategy.Continue(this, resident, character, target, session.Phase);
        return true;
    }

    public void ReleaseExecution(RegisteredNpcData resident, OccupationSession session, bool detachIfAttached = true)
    {
        var target = session.Target;

        if (!_runtimeService.TryGetBoundCharacter(resident.Id, out var character))
        {
            return;
        }

        if (_sustainStrategyRegistry.TryGetStrategy(target.Execution.StrategyId, out var sustainStrategy))
        {
            sustainStrategy.Release(this, resident, character, target, session);
        }

        if (_lifecycleStrategyRegistry.TryGetStrategy(target.Execution.StrategyId, out var lifecycleStrategy))
        {
            lifecycleStrategy.Release(this, resident, character, target);
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
        if (_waypointService.TryBuildRoute(character.transform.position, target.Plan.ApproachPosition, out var routePoints) && routePoints.Count > 0)
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

    public bool IsNearEngagePosition(Character character, OccupationTarget target, float maxDistance)
    {
        var delta = target.Plan.EngagePosition - GetOccupationReferenceWorldPosition(character);
        delta.y = 0f;
        return delta.sqrMagnitude <= maxDistance * maxDistance;
    }

    public bool IsNearApproachPosition(Character character, OccupationTarget target, float maxDistance)
    {
        var delta = target.Plan.ApproachPosition - GetOccupationReferenceWorldPosition(character);
        delta.y = 0f;
        return delta.sqrMagnitude <= maxDistance * maxDistance;
    }

    public float GetHorizontalDistance(Character character, Vector3 position)
    {
        var delta = position - GetOccupationReferenceWorldPosition(character);
        delta.y = 0f;
        return delta.magnitude;
    }

    private static Vector3 GetOccupationReferenceWorldPosition(Character character)
    {
        return character is WyrdrasilVikingNpc viking
            ? viking.GetWorkbenchPoseReferenceWorldPosition()
            : character.transform.position;
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

        if (target.Execution.IsCraftStation)
        {
            return IsNearEngagePosition(character, target, target.Plan.SustainRadius) &&
                   !_navigationService.IsNavigationActive(character);
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
