using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Wyrdrasil.Routines.Occupations;
using Wyrdrasil.Settlements.Services;
using Wyrdrasil.Settlements.Tool;
using Wyrdrasil.Souls.Services;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Routines.Services;

public sealed class ResidentOccupationService
{
    private sealed class WanderState
    {
        private const int RecentWaypointHistorySize = 3;
        private readonly Queue<int> _recentWaypointIds = new();

        public int? LastDestinationWaypointId { get; private set; }
        public float NextRouteSelectionTime { get; set; }
        public bool WasNavigating { get; set; }

        public bool WasRecentlyVisited(int waypointId)
        {
            return _recentWaypointIds.Contains(waypointId);
        }

        public void RegisterDestination(int waypointId)
        {
            LastDestinationWaypointId = waypointId;
            _recentWaypointIds.Enqueue(waypointId);

            while (_recentWaypointIds.Count > RecentWaypointHistorySize)
            {
                _recentWaypointIds.Dequeue();
            }
        }
    }

    private readonly ResidentRuntimeService _runtimeService;
    private readonly NavigationWaypointService _waypointService;
    private readonly NpcNavigationService _navigationService;
    private readonly OccupationExecutionService _executionService;
    private readonly OccupationResolverRegistry _resolverRegistry;
    private readonly Dictionary<int, WanderState> _wanderStatesByResidentId = new();
    private readonly Dictionary<int, OccupationSession> _sessionsByResidentId = new();

    public ResidentOccupationService(
        ResidentRuntimeService runtimeService,
        NavigationWaypointService waypointService,
        NpcNavigationService navigationService,
        OccupationExecutionService executionService,
        OccupationResolverRegistry resolverRegistry)
    {
        _runtimeService = runtimeService;
        _waypointService = waypointService;
        _navigationService = navigationService;
        _executionService = executionService;
        _resolverRegistry = resolverRegistry;
    }

    public bool TryStartOccupation(RegisteredNpcData resident, ResidentRoutineActivityType activityType)
    {
        if (activityType == ResidentRoutineActivityType.WanderBetweenWaypoints)
        {
            return TryStartOrContinueWandering(resident);
        }

        if (!_resolverRegistry.TryGetResolver(activityType, out var resolver))
        {
            return false;
        }

        if (!resolver.TryResolve(resident, out var target))
        {
            return false;
        }

        if (_executionService.TryBeginExecution(resident, target, out var phase))
        {
            _sessionsByResidentId[resident.Id] = new OccupationSession(activityType, target, phase);
            return true;
        }

        resolver.Release(resident);
        return false;
    }

    public void ContinueOccupation(RegisteredNpcData resident, ResidentRoutineActivityType activityType)
    {
        if (activityType == ResidentRoutineActivityType.WanderBetweenWaypoints)
        {
            TryStartOrContinueWandering(resident);
            return;
        }

        if (!_sessionsByResidentId.TryGetValue(resident.Id, out var session) || session.ActivityType != activityType)
        {
            TryStartOccupation(resident, activityType);
            return;
        }

        if (!_executionService.TryContinueExecution(resident, session.Target, session.Phase, out var nextPhase) ||
            nextPhase == OccupationPhase.None)
        {
            ReleaseOccupation(resident, true);
            return;
        }

        session.Phase = nextPhase;
    }

    public bool TryStartOrContinueWandering(RegisteredNpcData resident)
    {
        if (!_runtimeService.TryGetBoundCharacter(resident.Id, out var character))
        {
            return false;
        }

        if (_waypointService.Waypoints.Count == 0)
        {
            return false;
        }

        var wanderState = GetOrCreateWanderState(resident.Id);
        if (_navigationService.IsNavigationActive(character))
        {
            wanderState.WasNavigating = true;
            return true;
        }

        if (wanderState.WasNavigating)
        {
            wanderState.WasNavigating = false;
            wanderState.NextRouteSelectionTime = Time.time + Random.Range(1.2f, 3.2f);
            return true;
        }

        if (Time.time < wanderState.NextRouteSelectionTime)
        {
            return true;
        }

        var destination = SelectWanderDestination(wanderState, character.transform.position);
        if (destination == null)
        {
            return false;
        }

        wanderState.RegisterDestination(destination.Id);

        if (_waypointService.TryBuildRoute(character.transform.position, destination.Position, out var routePoints) && routePoints.Count > 0)
        {
            _navigationService.NavigateAlongRouteToPosition(character, routePoints, destination.Position, 0.35f);
            wanderState.WasNavigating = true;
            return true;
        }

        _navigationService.NavigateDirectlyToPosition(character, destination.Position, 0.4f);
        wanderState.WasNavigating = true;
        return true;
    }

    public void ReleaseOccupation(RegisteredNpcData resident, bool detachIfAttached = true)
    {
        _wanderStatesByResidentId.Remove(resident.Id);

        if (_sessionsByResidentId.TryGetValue(resident.Id, out var session))
        {
            _executionService.ReleaseExecution(resident, session.Target, detachIfAttached);
            _sessionsByResidentId.Remove(resident.Id);
        }
        else
        {
            _executionService.ReleaseResidentNavigation(resident, detachIfAttached);
        }

        foreach (var resolver in _resolverRegistry.Resolvers)
        {
            resolver.Release(resident);
        }
    }

    private WanderState GetOrCreateWanderState(int residentId)
    {
        if (_wanderStatesByResidentId.TryGetValue(residentId, out var state))
        {
            return state;
        }

        state = new WanderState();
        _wanderStatesByResidentId[residentId] = state;
        return state;
    }

    private NavigationWaypointData? SelectWanderDestination(WanderState state, Vector3 currentPosition)
    {
        var orderedWaypoints = _waypointService.Waypoints
            .OrderBy(candidate => HorizontalDistance(currentPosition, candidate.Position))
            .ToList();

        if (orderedWaypoints.Count == 0)
        {
            return null;
        }

        var currentNearestWaypointId = orderedWaypoints[0].Id;
        var candidates = orderedWaypoints
            .Where(candidate => candidate.Id != currentNearestWaypointId)
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        var weightedCandidates = new List<(NavigationWaypointData Waypoint, float Weight)>();
        foreach (var candidate in candidates)
        {
            var weight = ComputeWanderWeight(candidate, currentPosition, state);
            if (weight > 0.001f)
            {
                weightedCandidates.Add((candidate, weight));
            }
        }

        if (weightedCandidates.Count == 0)
        {
            return candidates[0];
        }

        var totalWeight = weightedCandidates.Sum(candidate => candidate.Weight);
        var randomValue = Random.Range(0f, totalWeight);
        foreach (var candidate in weightedCandidates)
        {
            randomValue -= candidate.Weight;
            if (randomValue <= 0f)
            {
                return candidate.Waypoint;
            }
        }

        return weightedCandidates[weightedCandidates.Count - 1].Waypoint;
    }

    private static float ComputeWanderWeight(NavigationWaypointData candidate, Vector3 currentPosition, WanderState state)
    {
        var distance = HorizontalDistance(currentPosition, candidate.Position);
        var normalizedDistance = Mathf.Clamp(distance, 4f, 26f);
        var weight = 0.75f + (normalizedDistance / 12f);

        if (state.LastDestinationWaypointId == candidate.Id)
        {
            weight *= 0.2f;
        }

        if (state.WasRecentlyVisited(candidate.Id))
        {
            weight *= 0.45f;
        }

        return weight;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        var delta = b - a;
        delta.y = 0f;
        return delta.magnitude;
    }
}
