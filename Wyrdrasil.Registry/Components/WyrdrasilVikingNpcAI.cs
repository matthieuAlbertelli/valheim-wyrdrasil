using System.Collections.Generic;
using UnityEngine;
using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Components;

public sealed class WyrdrasilVikingNpcAI : MonsterAI
{
    private const float IntermediateWaypointRadius = 1.0f;
    private const float SeatAlignDistance = 0.35f;
    private const float SeatAlignAngle = 6f;

    private enum NavigationState
    {
        Idle,
        Travelling,
        AligningToSeat,
        AttachingToSeat,
        Seated
    }

    private readonly List<Vector3> _routePoints = new();

    private WyrdrasilVikingNpc? _viking;
    private Rigidbody? _rigidbody;

    private RegisteredSeatData? _seatTarget;
    private Vector3 _finalFacingDirection;
    private float _finalStopDistance;
    private int _currentRouteIndex;
    private NavigationState _state = NavigationState.Idle;

    protected override void Awake()
    {
        base.Awake();

        _viking = GetComponent<WyrdrasilVikingNpc>();
        _rigidbody = GetComponent<Rigidbody>();
    }

    public void NavigateDirectly(Vector3 destination, float stopDistance, Vector3 finalFacingDirection)
    {
        _routePoints.Clear();
        _routePoints.Add(destination);

        _seatTarget = null;
        _finalStopDistance = stopDistance;
        _finalFacingDirection = finalFacingDirection.sqrMagnitude > 0.0001f
            ? finalFacingDirection.normalized
            : transform.forward;

        _currentRouteIndex = 0;
        _state = NavigationState.Travelling;
        enabled = true;
    }

    public void NavigateAlongRoute(IReadOnlyList<Vector3> routePoints, Vector3 finalDestination, float stopDistance, Vector3 finalFacingDirection)
    {
        _routePoints.Clear();

        foreach (var routePoint in routePoints)
        {
            _routePoints.Add(routePoint);
        }

        _routePoints.Add(finalDestination);

        _seatTarget = null;
        _finalStopDistance = stopDistance;
        _finalFacingDirection = finalFacingDirection.sqrMagnitude > 0.0001f
            ? finalFacingDirection.normalized
            : transform.forward;

        _currentRouteIndex = 0;
        _state = NavigationState.Travelling;
        enabled = true;
    }

    public void NavigateDirectlyToSeat(RegisteredSeatData seat, float stopDistance)
    {
        _routePoints.Clear();
        _routePoints.Add(seat.ApproachPosition);

        _seatTarget = seat;
        _finalStopDistance = stopDistance;
        _finalFacingDirection = seat.SeatForward.sqrMagnitude > 0.0001f
            ? seat.SeatForward.normalized
            : transform.forward;

        _currentRouteIndex = 0;
        _state = NavigationState.Travelling;
        enabled = true;
    }

    public void NavigateAlongRouteToSeat(IReadOnlyList<Vector3> routePoints, RegisteredSeatData seat, float stopDistance)
    {
        _routePoints.Clear();

        foreach (var routePoint in routePoints)
        {
            _routePoints.Add(routePoint);
        }

        _routePoints.Add(seat.ApproachPosition);

        _seatTarget = seat;
        _finalStopDistance = stopDistance;
        _finalFacingDirection = seat.SeatForward.sqrMagnitude > 0.0001f
            ? seat.SeatForward.normalized
            : transform.forward;

        _currentRouteIndex = 0;
        _state = NavigationState.Travelling;
        enabled = true;
    }

    public void ClearNavigation()
    {
        _routePoints.Clear();
        _seatTarget = null;
        _currentRouteIndex = 0;
        _state = NavigationState.Idle;
        StopMoving();
        ZeroVelocity();
    }

    public override bool UpdateAI(float dt)
    {
        if (_viking == null)
        {
            return true;
        }

        if (_viking.IsAttached())
        {
            _state = NavigationState.Seated;
            StopMoving();
            ZeroVelocity();
            return true;
        }

        switch (_state)
        {
            case NavigationState.Travelling:
                UpdateTravelling(dt);
                break;

            case NavigationState.AligningToSeat:
                UpdateAligningToSeat();
                break;

            case NavigationState.AttachingToSeat:
                UpdateAttachingToSeat();
                break;

            case NavigationState.Seated:
                StopMoving();
                ZeroVelocity();
                break;

            case NavigationState.Idle:
            default:
                StopMoving();
                ZeroVelocity();
                break;
        }

        return true;
    }

    private void UpdateTravelling(float dt)
    {
        if (_currentRouteIndex >= _routePoints.Count)
        {
            FinishTravel();
            return;
        }

        var targetPoint = _routePoints[_currentRouteIndex];
        var reachRadius = _currentRouteIndex == _routePoints.Count - 1
            ? _finalStopDistance
            : IntermediateWaypointRadius;

        if (HasReachedHorizontally(targetPoint, reachRadius))
        {
            _currentRouteIndex++;

            if (_currentRouteIndex >= _routePoints.Count)
            {
                FinishTravel();
            }

            return;
        }

        RotateBodyTowards(targetPoint, dt);
        MoveTo(dt, targetPoint, reachRadius, false);
    }

    private void FinishTravel()
    {
        StopMoving();
        ZeroVelocity();

        if (_seatTarget != null)
        {
            _state = NavigationState.AligningToSeat;
            return;
        }

        _state = NavigationState.Idle;
    }

    private void UpdateAligningToSeat()
    {
        if (_seatTarget == null)
        {
            _state = NavigationState.Idle;
            return;
        }

        StopMoving();
        ZeroVelocity();

        var approachPosition = _seatTarget.ApproachPosition;
        var seatForward = _seatTarget.SeatForward.sqrMagnitude > 0.0001f
            ? _seatTarget.SeatForward.normalized
            : transform.forward;

        transform.position = approachPosition;

        var targetRotation = Quaternion.LookRotation(seatForward, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, 720f * Time.deltaTime);

        var remainingAngle = Quaternion.Angle(transform.rotation, targetRotation);
        var remainingDistance = GetHorizontalDistanceToPoint(approachPosition);

        if (remainingDistance <= SeatAlignDistance && remainingAngle <= SeatAlignAngle)
        {
            _state = NavigationState.AttachingToSeat;
        }
    }

    private void UpdateAttachingToSeat()
    {
        if (_seatTarget == null || _viking == null || _seatTarget.ChairComponent == null)
        {
            _state = NavigationState.Idle;
            return;
        }

        StopMoving();
        ZeroVelocity();

        transform.position = _seatTarget.ApproachPosition;
        transform.rotation = Quaternion.LookRotation(_finalFacingDirection, Vector3.up);

        _viking.AttachToChair(_seatTarget.ChairComponent);

        if (_viking.IsAttached())
        {
            _state = NavigationState.Seated;
            return;
        }

        // Fallback plus agressif : on force le snap exact sur le point du siège,
        // puis on retente immédiatement l'attache native.
        transform.position = _seatTarget.SeatPosition;
        transform.rotation = Quaternion.LookRotation(_finalFacingDirection, Vector3.up);

        _viking.AttachToChair(_seatTarget.ChairComponent);

        _state = _viking.IsAttached()
            ? NavigationState.Seated
            : NavigationState.Idle;
    }

    private void RotateBodyTowards(Vector3 targetPoint, float dt)
    {
        var direction = targetPoint - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        var targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, 360f * dt);
    }

    private bool HasReachedHorizontally(Vector3 targetPoint, float radius)
    {
        return GetHorizontalDistanceToPoint(targetPoint) <= radius;
    }

    private float GetHorizontalDistanceToPoint(Vector3 targetPoint)
    {
        var delta = targetPoint - transform.position;
        delta.y = 0f;
        return delta.magnitude;
    }

    private void ZeroVelocity()
    {
        if (_rigidbody == null)
        {
            return;
        }

        _rigidbody.linearVelocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
    }
}