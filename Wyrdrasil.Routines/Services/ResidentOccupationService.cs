using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using UnityEngine;
using Wyrdrasil.Registry.Tool;
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

    private readonly ManualLogSource _log;
    private readonly ResidentRuntimeService _runtimeService;
    private readonly ZoneSlotService _slotService;
    private readonly SeatService _seatService;
    private readonly BedService _bedService;
    private readonly NpcNavigationService _navigationService;
    private readonly NavigationWaypointService _waypointService;
    private readonly Dictionary<int, WanderState> _wanderStatesByResidentId = new();

    public ResidentOccupationService(
        ManualLogSource log,
        ResidentRuntimeService runtimeService,
        ZoneSlotService slotService,
        SeatService seatService,
        BedService bedService,
        NpcNavigationService navigationService,
        NavigationWaypointService waypointService)
    {
        _log = log;
        _runtimeService = runtimeService;
        _slotService = slotService;
        _seatService = seatService;
        _bedService = bedService;
        _navigationService = navigationService;
        _waypointService = waypointService;
    }

    public bool TryOccupyAssignedSlot(RegisteredNpcData resident)
    {
        if (!resident.AssignedSlotId.HasValue)
        {
            return false;
        }

        if (!_slotService.TryGetSlotById(resident.AssignedSlotId.Value, out var slotData))
        {
            return false;
        }

        if (!_runtimeService.TryGetBoundCharacter(resident.Id, out var character))
        {
            return false;
        }

        if (_waypointService.TryBuildRoute(character.transform.position, slotData.Position, out var routePoints) && routePoints.Count > 0)
        {
            _navigationService.NavigateAlongRouteToPosition(character, routePoints, slotData.Position);
            return true;
        }

        _navigationService.NavigateDirectlyToPosition(character, slotData.Position);
        return true;
    }

    public bool TryOccupyAssignedSeat(RegisteredNpcData resident)
    {
        if (!resident.AssignedSeatId.HasValue)
        {
            return false;
        }

        if (!_seatService.TryGetSeatById(resident.AssignedSeatId.Value, out var seatData))
        {
            return false;
        }

        if (!_runtimeService.TryGetBoundCharacter(resident.Id, out var character))
        {
            return false;
        }

        if (_waypointService.TryBuildRoute(character.transform.position, seatData.ApproachPosition, out var routePoints) && routePoints.Count > 0)
        {
            _navigationService.NavigateAlongRouteToSeat(character, routePoints, seatData);
            return true;
        }

        _navigationService.NavigateDirectlyToSeat(character, seatData);
        return true;
    }

    public bool TryOccupyAvailablePublicSeat(RegisteredNpcData resident)
    {
        if (!_seatService.TryReservePublicTavernSeat(resident.Id, out var seatData) || seatData == null)
        {
            return false;
        }

        if (!_runtimeService.TryGetBoundCharacter(resident.Id, out var character))
        {
            _seatService.ReleasePublicSeatOccupation(resident.Id);
            return false;
        }

        if (_waypointService.TryBuildRoute(character.transform.position, seatData.ApproachPosition, out var routePoints) && routePoints.Count > 0)
        {
            _navigationService.NavigateAlongRouteToSeat(character, routePoints, seatData);
            return true;
        }

        _navigationService.NavigateDirectlyToSeat(character, seatData);
        return true;
    }

    public bool TryOccupyAssignedBed(RegisteredNpcData resident)
    {
        if (!resident.AssignedBedId.HasValue)
        {
            return false;
        }

        if (!_bedService.TryGetBedById(resident.AssignedBedId.Value, out var bedData))
        {
            return false;
        }

        if (!_runtimeService.TryGetBoundCharacter(resident.Id, out var character))
        {
            return false;
        }

        if (_waypointService.TryBuildRoute(character.transform.position, bedData.ApproachPosition, out var routePoints) && routePoints.Count > 0)
        {
            _navigationService.NavigateAlongRouteToBed(character, routePoints, bedData);
            return true;
        }

        _navigationService.NavigateDirectlyToBed(character, bedData);
        return true;
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
        _seatService.ReleasePublicSeatOccupation(resident.Id);

        if (_runtimeService.TryGetBoundCharacter(resident.Id, out var character))
        {
            _navigationService.ReleaseOccupation(character, detachIfAttached);
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
