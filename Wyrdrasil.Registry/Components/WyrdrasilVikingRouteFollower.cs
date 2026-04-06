using System.Collections.Generic;
using UnityEngine;
using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Components;

/// <summary>
/// Owns waypoint traversal for registry vikings.
/// Principles:
/// - strict ordered traversal, one waypoint at a time
/// - no shortcuts to future waypoints
/// - no segment lookahead heuristics
/// - handoff to seat logic only after the final waypoint has been consumed
/// </summary>
public sealed class WyrdrasilVikingRouteFollower : MonoBehaviour
{
    private const float WaypointReachRadius = 0.85f;
    private const float FinalDestinationReachRadius = 0.35f;

    private enum RouteMode
    {
        None,
        TraverseWaypoints,
        DirectFinalDestination
    }

    private readonly List<Vector3> _routePoints = new();

    private WyrdrasilVikingNpcAI? _vikingAi;
    private RegisteredSeatData? _seatTarget;
    private Vector3 _finalDestination;
    private Vector3 _finalFacingDirection;
    private float _finalStopDistance;
    private int _currentRouteIndex;
    private RouteMode _routeMode = RouteMode.None;

    private void Awake()
    {
        _vikingAi = GetComponent<WyrdrasilVikingNpcAI>();
    }

    public void ConfigureRouteToPosition(
        IReadOnlyList<Vector3> routePoints,
        Vector3 finalDestination,
        float finalStopDistance,
        Vector3 finalFacingDirection)
    {
        enabled = true;
        _routePoints.Clear();
        AppendRoutePoints(routePoints);

        _seatTarget = null;
        _finalDestination = finalDestination;
        _finalStopDistance = Mathf.Max(finalStopDistance, FinalDestinationReachRadius);
        _finalFacingDirection = finalFacingDirection.sqrMagnitude > 0.0001f
            ? finalFacingDirection.normalized
            : transform.forward;

        _currentRouteIndex = 0;
        SkipAlreadyReachedWaypoints();

        _routeMode = _routePoints.Count > 0 && _currentRouteIndex < _routePoints.Count
            ? RouteMode.TraverseWaypoints
            : RouteMode.DirectFinalDestination;
    }

    public void ConfigureRouteToSeat(IReadOnlyList<Vector3> routePoints, RegisteredSeatData seat)
    {
        enabled = true;
        _routePoints.Clear();
        AppendRoutePoints(routePoints);

        _seatTarget = seat;
        _finalDestination = Vector3.zero;
        _finalStopDistance = 0f;
        _finalFacingDirection = seat.SeatForward.sqrMagnitude > 0.0001f
            ? seat.SeatForward.normalized
            : transform.forward;

        _currentRouteIndex = 0;
        SkipAlreadyReachedWaypoints();

        if (_routePoints.Count > 0 && _currentRouteIndex < _routePoints.Count)
        {
            _routeMode = RouteMode.TraverseWaypoints;
            return;
        }

        StartSeatHandoff();
    }

    public void ReleaseControl()
    {
        _routePoints.Clear();
        _seatTarget = null;
        _currentRouteIndex = 0;
        _routeMode = RouteMode.None;
        enabled = false;
    }

    private void Update()
    {
        if (_vikingAi == null)
        {
            return;
        }

        switch (_routeMode)
        {
            case RouteMode.TraverseWaypoints:
                UpdateWaypointTraversal();
                break;

            case RouteMode.DirectFinalDestination:
                UpdateFinalDestinationApproach();
                break;
        }
    }

    private void UpdateWaypointTraversal()
    {
        if (_currentRouteIndex >= _routePoints.Count)
        {
            HandleRouteCompletion();
            return;
        }

        var targetWaypoint = _routePoints[_currentRouteIndex];
        if (HasReachedHorizontally(targetWaypoint, WaypointReachRadius))
        {
            _currentRouteIndex++;

            if (_currentRouteIndex >= _routePoints.Count)
            {
                HandleRouteCompletion();
                return;
            }

            targetWaypoint = _routePoints[_currentRouteIndex];
        }

        _vikingAi.SetSteeringTarget(
            targetWaypoint,
            WaypointReachRadius,
            targetWaypoint - transform.position);
    }

    private void UpdateFinalDestinationApproach()
    {
        if (HasReachedHorizontally(_finalDestination, _finalStopDistance))
        {
            _vikingAi.ClearSteering();
            _routeMode = RouteMode.None;
            enabled = false;
            return;
        }

        _vikingAi.SetSteeringTarget(
            _finalDestination,
            _finalStopDistance,
            _finalFacingDirection);
    }

    private void HandleRouteCompletion()
    {
        if (_seatTarget != null)
        {
            StartSeatHandoff();
            return;
        }

        _routeMode = RouteMode.DirectFinalDestination;
        UpdateFinalDestinationApproach();
    }

    private void StartSeatHandoff()
    {
        if (_vikingAi == null || _seatTarget == null)
        {
            _routeMode = RouteMode.None;
            enabled = false;
            return;
        }

        _vikingAi.StartSeatApproach(_seatTarget, true);
        _routeMode = RouteMode.None;
        enabled = false;
    }

    private bool HasReachedHorizontally(Vector3 targetPoint, float radius)
    {
        var delta = targetPoint - transform.position;
        delta.y = 0f;
        return delta.magnitude <= radius;
    }

    private void AppendRoutePoints(IReadOnlyList<Vector3> routePoints)
    {
        foreach (var point in routePoints)
        {
            AppendPoint(point);
        }
    }

    private void AppendPoint(Vector3 point)
    {
        if (_routePoints.Count == 0)
        {
            _routePoints.Add(point);
            return;
        }

        var previous = _routePoints[_routePoints.Count - 1];
        var horizontalDelta = point - previous;
        horizontalDelta.y = 0f;

        if (horizontalDelta.sqrMagnitude > 0.01f)
        {
            _routePoints.Add(point);
        }
    }

    private void SkipAlreadyReachedWaypoints()
    {
        while (_currentRouteIndex < _routePoints.Count)
        {
            if (!HasReachedHorizontally(_routePoints[_currentRouteIndex], WaypointReachRadius))
            {
                break;
            }

            _currentRouteIndex++;
        }
    }
}
