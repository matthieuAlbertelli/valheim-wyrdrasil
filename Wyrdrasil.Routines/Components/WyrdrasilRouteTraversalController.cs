using System.Collections.Generic;
using UnityEngine;
using Wyrdrasil.Core.Tool;
using Wyrdrasil.Routines.Occupations;
using Wyrdrasil.Souls.Components;

namespace Wyrdrasil.Routines.Components;

/// <summary>
/// Dedicated Registry route executor.
/// It owns the waypoint sequence and delegates actual locomotion to WyrdrasilVikingNpcAI steering,
/// while validating progression through segment-based capture instead of exact node centering.
/// </summary>
public sealed class WyrdrasilRouteTraversalController : MonoBehaviour
{
    private const float InitialWaypointSteeringStopDistance = 0.65f;
    private const float WaypointSteeringStopDistance = 0.45f;

    private const float InitialSegmentConsumeRadius = 1.00f;
    private const float InitialSegmentRelaxedConsumeRadius = 1.45f;
    private const float InitialSegmentCorridorRadius = 1.25f;
    private const float InitialSegmentEndPlaneSlack = 0.30f;
    private const float InitialSegmentNoProgressTimeout = 1.25f;

    private const float SegmentConsumeRadius = 0.70f;
    private const float SegmentRelaxedConsumeRadius = 1.05f;
    private const float SegmentCorridorRadius = 0.95f;
    private const float SegmentEndPlaneSlack = 0.20f;
    private const float SegmentNoProgressTimeout = 0.90f;

    private const float FinalDestinationReachRadius = 0.35f;
    private const float VerticalArrivalTolerance = 0.35f;
    private const float ProgressEpsilon = 0.05f;

    private enum TraversalMode
    {
        None,
        TraverseWaypoints,
        TraverseFinalDestination
    }

    private readonly List<Vector3> _routePoints = new();

    private Humanoid? _humanoid;
    private WyrdrasilVikingNpcAI? _ai;
    private OccupationAnchorNavigationPlan? _anchorHandoff;
    private Vector3 _finalDestination;
    private Vector3 _finalFacingDirection;
    private float _finalStopDistance;
    private Vector3 _currentSegmentStart;
    private int _currentRouteIndex;
    private int _announcedRouteIndex = -1;
    private bool _finalTargetIssued;
    private float _bestDistanceToCurrentWaypoint = float.MaxValue;
    private float _waypointNoProgressTimer;
    private TraversalMode _mode = TraversalMode.None;

    public bool IsTraversalActive => enabled && _mode != TraversalMode.None;

    private void Awake()
    {
        _humanoid = GetComponent<Humanoid>();
        _ai = GetComponent<WyrdrasilVikingNpcAI>();
    }

    public void ConfigureRouteToPosition(
        IReadOnlyList<Vector3> routePoints,
        Vector3 finalDestination,
        float finalStopDistance,
        Vector3 finalFacingDirection)
    {
        PrepareForTraversal();

        _routePoints.Clear();
        AppendRoutePoints(routePoints);

        ClearAnchorHandoff();
        _finalDestination = finalDestination;
        _finalStopDistance = Mathf.Max(finalStopDistance, FinalDestinationReachRadius);
        _finalFacingDirection = finalFacingDirection.sqrMagnitude > 0.0001f
            ? finalFacingDirection.normalized
            : transform.forward;

        ResetTraversalState();
        BeginTraversal();
    }

    public void ConfigureRouteToSeat(
        IReadOnlyList<Vector3> routePoints,
        Vector3 approachPosition,
        Vector3 seatUsePosition,
        Vector3 seatFacingDirection,
        Chair? chairComponent,
        OccupationAnchorApproachProfile? approachProfile = null)
    {
        ConfigureRouteToAnchor(
            routePoints,
            OccupationAnchorNavigationPlan.Seat(
                approachPosition,
                seatUsePosition,
                seatFacingDirection,
                chairComponent,
                approachProfile ?? OccupationAnchorApproachProfile.SeatDefault));
    }

    public void ConfigureRouteToBed(
        IReadOnlyList<Vector3> routePoints,
        Vector3 approachPosition,
        Vector3 bedUsePosition,
        Vector3 bedFacingDirection,
        Bed? bedComponent,
        Transform? bedAttachPoint,
        OccupationAnchorApproachProfile? approachProfile = null)
    {
        ConfigureRouteToAnchor(
            routePoints,
            OccupationAnchorNavigationPlan.Bed(
                approachPosition,
                bedUsePosition,
                bedFacingDirection,
                bedComponent,
                bedAttachPoint,
                approachProfile ?? OccupationAnchorApproachProfile.BedDefault));
    }

    public void ConfigureRouteToAnchor(
        IReadOnlyList<Vector3> routePoints,
        OccupationAnchorNavigationPlan anchorPlan)
    {
        PrepareForTraversal();

        _routePoints.Clear();
        AppendRoutePoints(routePoints);

        _anchorHandoff = anchorPlan;
        _finalDestination = anchorPlan.ApproachPosition;
        _finalStopDistance = anchorPlan.ApproachProfile.ApproachRadius;
        _finalFacingDirection = anchorPlan.FacingDirection;

        ResetTraversalState();
        BeginTraversal();
    }

    public void ReleaseControl()
    {
        _routePoints.Clear();
        ClearAnchorHandoff();
        _currentRouteIndex = 0;
        _announcedRouteIndex = -1;
        _finalTargetIssued = false;
        _bestDistanceToCurrentWaypoint = float.MaxValue;
        _waypointNoProgressTimer = 0f;
        _mode = TraversalMode.None;

        if (_ai != null)
        {
            _ai.ClearSteering();
        }

        enabled = false;
    }

    private void Update()
    {
        switch (_mode)
        {
            case TraversalMode.TraverseWaypoints:
                UpdateWaypointTraversal();
                break;
            case TraversalMode.TraverseFinalDestination:
                UpdateFinalDestinationApproach();
                break;
        }
    }

    private void PrepareForTraversal()
    {
        DetachIfNeeded();

        if (_ai != null)
        {
            _ai.ClearSteering();
        }
    }

    private void ResetTraversalState()
    {
        _currentSegmentStart = transform.position;
        _currentRouteIndex = 0;
        _announcedRouteIndex = -1;
        _finalTargetIssued = false;
        _bestDistanceToCurrentWaypoint = float.MaxValue;
        _waypointNoProgressTimer = 0f;
        SkipAlreadyReachedWaypoints();
    }

    private void BeginTraversal()
    {
        _mode = _routePoints.Count > 0 && _currentRouteIndex < _routePoints.Count
            ? TraversalMode.TraverseWaypoints
            : TraversalMode.TraverseFinalDestination;

        _finalTargetIssued = false;
        enabled = true;
    }

    private void UpdateWaypointTraversal()
    {
        if (_ai == null)
        {
            _mode = TraversalMode.None;
            enabled = false;
            return;
        }

        if (_currentRouteIndex >= _routePoints.Count)
        {
            HandleRouteCompletion();
            return;
        }

        var waypoint = _routePoints[_currentRouteIndex];

        if (_announcedRouteIndex != _currentRouteIndex)
        {
            _announcedRouteIndex = _currentRouteIndex;
            _bestDistanceToCurrentWaypoint = float.MaxValue;
            _waypointNoProgressTimer = 0f;

            var steeringStopDistance = _currentRouteIndex == 0
                ? InitialWaypointSteeringStopDistance
                : WaypointSteeringStopDistance;

            _ai.SetSteeringTarget(waypoint, steeringStopDistance, waypoint - transform.position);
        }

        if (!HasConsumedCurrentWaypoint(waypoint))
        {
            return;
        }

        _currentSegmentStart = waypoint;
        _currentRouteIndex++;
        _announcedRouteIndex = -1;

        if (_currentRouteIndex >= _routePoints.Count)
        {
            HandleRouteCompletion();
        }
    }

    private void UpdateFinalDestinationApproach()
    {
        if (_ai == null)
        {
            _mode = TraversalMode.None;
            enabled = false;
            return;
        }

        if (!_finalTargetIssued)
        {
            _ai.SetSteeringTarget(_finalDestination, _finalStopDistance, _finalFacingDirection);
            _finalTargetIssued = true;
        }

        if (!HasReachedFinalDestination())
        {
            return;
        }

        _ai.ClearSteering();

        if (_anchorHandoff.HasValue)
        {
            StartAnchorHandoff(_anchorHandoff.Value);
            return;
        }

        _mode = TraversalMode.None;
        enabled = false;
    }

    private void HandleRouteCompletion()
    {
        _mode = TraversalMode.TraverseFinalDestination;
        _finalTargetIssued = false;
    }

    private void StartAnchorHandoff(OccupationAnchorNavigationPlan anchorPlan)
    {
        if (_ai == null || anchorPlan.AnchorKind == OccupationAnchorAttachmentKind.None)
        {
            _mode = TraversalMode.None;
            enabled = false;
            return;
        }

        _ai.StartAttachmentAnchorApproach(
            anchorPlan.AnchorKind,
            anchorPlan.ApproachPosition,
            anchorPlan.EngagePosition,
            anchorPlan.FacingDirection,
            anchorPlan.ChairComponent,
            anchorPlan.BedComponent,
            anchorPlan.AttachPoint,
            arrivedFromWaypointRoute: true,
            approachProfile: anchorPlan.ApproachProfile);

        _mode = TraversalMode.None;
        enabled = false;
    }

    private bool HasConsumedCurrentWaypoint(Vector3 waypoint)
    {
        var horizontalDistance = HorizontalDistanceTo(waypoint);

        if (horizontalDistance < _bestDistanceToCurrentWaypoint - ProgressEpsilon)
        {
            _bestDistanceToCurrentWaypoint = horizontalDistance;
            _waypointNoProgressTimer = 0f;
        }
        else
        {
            _waypointNoProgressTimer += Time.deltaTime;
        }

        if (HasReachedPoint(waypoint, GetConsumeRadius()))
        {
            return true;
        }

        if (HasCrossedSegmentGate(_currentSegmentStart, waypoint))
        {
            return true;
        }

        if (_waypointNoProgressTimer >= GetNoProgressTimeout() &&
            HasReachedPoint(waypoint, GetRelaxedConsumeRadius()))
        {
            return true;
        }

        return false;
    }

    private bool HasCrossedSegmentGate(Vector3 segmentStart, Vector3 segmentEnd)
    {
        var segmentVector = segmentEnd - segmentStart;
        segmentVector.y = 0f;
        var segmentLength = segmentVector.magnitude;

        if (segmentLength <= 0.001f)
        {
            return true;
        }

        var segmentDirection = segmentVector / segmentLength;
        var npcPosition = transform.position;
        var fromStartToNpc = npcPosition - segmentStart;
        fromStartToNpc.y = 0f;

        var projectedDistance = Vector3.Dot(fromStartToNpc, segmentDirection);
        var projectedPoint = segmentStart + segmentDirection * Mathf.Clamp(projectedDistance, 0f, segmentLength);
        var lateralOffset = npcPosition - projectedPoint;
        lateralOffset.y = 0f;

        return projectedDistance >= segmentLength - GetSegmentEndPlaneSlack()
               && lateralOffset.magnitude <= GetSegmentCorridorRadius();
    }

    private float GetConsumeRadius()
    {
        return _currentRouteIndex == 0 ? InitialSegmentConsumeRadius : SegmentConsumeRadius;
    }

    private float GetRelaxedConsumeRadius()
    {
        return _currentRouteIndex == 0 ? InitialSegmentRelaxedConsumeRadius : SegmentRelaxedConsumeRadius;
    }

    private float GetNoProgressTimeout()
    {
        return _currentRouteIndex == 0 ? InitialSegmentNoProgressTimeout : SegmentNoProgressTimeout;
    }

    private float GetSegmentCorridorRadius()
    {
        return _currentRouteIndex == 0 ? InitialSegmentCorridorRadius : SegmentCorridorRadius;
    }

    private float GetSegmentEndPlaneSlack()
    {
        return _currentRouteIndex == 0 ? InitialSegmentEndPlaneSlack : SegmentEndPlaneSlack;
    }

    private float HorizontalDistanceTo(Vector3 targetPoint)
    {
        var delta = targetPoint - transform.position;
        delta.y = 0f;
        return delta.magnitude;
    }

    private void DetachIfNeeded()
    {
        if (_humanoid == null || !_humanoid.IsAttached())
        {
            return;
        }

        _humanoid.AttachStop();
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
            if (!HasReachedPoint(_routePoints[_currentRouteIndex], GetConsumeRadius()))
            {
                break;
            }

            _currentSegmentStart = _routePoints[_currentRouteIndex];
            _currentRouteIndex++;
        }
    }

    private bool HasReachedFinalDestination()
    {
        if (_anchorHandoff.HasValue)
        {
            var anchorPlan = _anchorHandoff.Value;
            return HasReachedPoint(_finalDestination, _finalStopDistance) ||
                   anchorPlan.ApproachProfile.CanForceAttempt(transform.position, anchorPlan.ApproachPosition, anchorPlan.EngagePosition);
        }

        return HasReachedPoint(_finalDestination, _finalStopDistance);
    }

    private bool HasReachedPoint(Vector3 targetPoint, float arrivalRadius)
    {
        var delta = targetPoint - transform.position;
        var verticalDistance = Mathf.Abs(delta.y);
        delta.y = 0f;
        return delta.magnitude <= arrivalRadius && verticalDistance <= VerticalArrivalTolerance;
    }

    private void ClearAnchorHandoff()
    {
        _anchorHandoff = null;
    }
}
