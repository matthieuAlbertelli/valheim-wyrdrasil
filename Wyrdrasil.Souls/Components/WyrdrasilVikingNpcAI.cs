using UnityEngine;
using Wyrdrasil.Registry.Diagnostics;
using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Components;

public sealed class WyrdrasilVikingNpcAI : MonsterAI
{
    private const float SeatApproachRadius = 0.95f;
    private const float BedApproachRadius = 0.95f;
    private const float AttemptRetryInterval = 0.25f;
    private const float ApproachProgressEpsilon = 0.10f;
    private const float ApproachTimeoutDirect = 1.35f;
    private const float ApproachTimeoutFromRoute = 0.75f;
    private const float ApproachStuckTimeoutDirect = 0.90f;
    private const float ApproachStuckTimeoutFromRoute = 0.50f;

    private enum NavigationMode
    {
        Idle,
        Steering,
        SeatApproach,
        SeatAttempt,
        Seated,
        BedApproach,
        BedAttempt,
        Sleeping
    }

    private WyrdrasilVikingNpc? _viking;
    private Rigidbody? _rigidbody;

    private Vector3 _steeringTarget;
    private float _steeringStopDistance;
    private Vector3 _steeringFacingDirection;
    private bool _hasSteeringTarget;

    private RegisteredSeatData? _seatTarget;
    private RegisteredBedData? _bedTarget;
    private bool _travelLocked;

    private Vector3 _approachPoint;
    private float _approachElapsed;
    private float _approachTimeout;
    private float _approachStuckTimer;
    private float _approachStuckTimeout;
    private float _bestApproachDistance;
    private float _nextAttemptTime;
    private NavigationMode _mode = NavigationMode.Idle;

    protected override void Awake()
    {
        base.Awake();
        _viking = GetComponent<WyrdrasilVikingNpc>();
        _rigidbody = GetComponent<Rigidbody>();
    }

    public void EnterRegistryTravelLock()
    {
        _travelLocked = true;
        _hasSteeringTarget = false;
        _seatTarget = null;
        _bedTarget = null;
        _mode = NavigationMode.Idle;
        StopMoving();
        ZeroVelocity();
        enabled = false;
    }

    public void ExitRegistryTravelLock()
    {
        _travelLocked = false;
        ClearSteering();
        enabled = true;
    }

    public void SetSteeringTarget(Vector3 targetPosition, float stopDistance, Vector3 facingDirection)
    {
        _travelLocked = false;
        _steeringTarget = targetPosition;
        _steeringStopDistance = stopDistance;
        _steeringFacingDirection = facingDirection.sqrMagnitude > 0.0001f
            ? facingDirection.normalized
            : transform.forward;

        _seatTarget = null;
        _bedTarget = null;
        _hasSteeringTarget = true;
        _mode = NavigationMode.Steering;
        enabled = true;
    }

    public void ClearSteering()
    {
        _hasSteeringTarget = false;
        _seatTarget = null;
        _bedTarget = null;
        _mode = _viking != null && _viking.IsAttached() ? NavigationMode.Seated : NavigationMode.Idle;
        StopMoving();
        ZeroVelocity();
    }

    public void StartSeatApproach(RegisteredSeatData seat, bool arrivedFromWaypointRoute = false)
    {
        StartOccupiedAnchorApproach(seat.ApproachPosition, arrivedFromWaypointRoute);
        _seatTarget = seat;
        _bedTarget = null;
        _mode = NavigationMode.SeatApproach;
    }

    public void StartBedApproach(RegisteredBedData bed, bool arrivedFromWaypointRoute = false)
    {
        StartOccupiedAnchorApproach(bed.ApproachPosition, arrivedFromWaypointRoute);
        _seatTarget = null;
        _bedTarget = bed;
        _mode = NavigationMode.BedApproach;
    }

    public override bool UpdateAI(float dt)
    {
        if (_viking == null)
        {
            return true;
        }

        if (_travelLocked)
        {
            StopMoving();
            ZeroVelocity();
            return true;
        }

        if (_viking.IsAttached())
        {
            _mode = _bedTarget != null ? NavigationMode.Sleeping : NavigationMode.Seated;
            StopMoving();
            ZeroVelocity();
            return true;
        }

        switch (_mode)
        {
            case NavigationMode.Steering:
                UpdateSteering(dt);
                break;
            case NavigationMode.SeatApproach:
                UpdateSeatApproach(dt);
                break;
            case NavigationMode.SeatAttempt:
                UpdateSeatAttempt(dt);
                break;
            case NavigationMode.BedApproach:
                UpdateBedApproach(dt);
                break;
            case NavigationMode.BedAttempt:
                UpdateBedAttempt(dt);
                break;
            case NavigationMode.Seated:
            case NavigationMode.Sleeping:
                StopMoving();
                ZeroVelocity();
                break;
            case NavigationMode.Idle:
            default:
                StopMoving();
                ZeroVelocity();
                break;
        }

        return true;
    }

    private void StartOccupiedAnchorApproach(Vector3 approachPoint, bool arrivedFromWaypointRoute)
    {
        _travelLocked = false;
        _approachPoint = approachPoint;
        _approachElapsed = 0f;
        _approachTimeout = arrivedFromWaypointRoute ? ApproachTimeoutFromRoute : ApproachTimeoutDirect;
        _approachStuckTimeout = arrivedFromWaypointRoute ? ApproachStuckTimeoutFromRoute : ApproachStuckTimeoutDirect;
        _approachStuckTimer = 0f;
        _bestApproachDistance = float.MaxValue;
        _nextAttemptTime = 0f;
        _hasSteeringTarget = false;
        enabled = true;
    }

    private void UpdateSteering(float dt)
    {
        if (!_hasSteeringTarget)
        {
            _mode = NavigationMode.Idle;
            StopMoving();
            ZeroVelocity();
            return;
        }

        if (HasReachedHorizontally(_steeringTarget, _steeringStopDistance))
        {
            StopMoving();
            ZeroVelocity();
            RotateTowards(_steeringFacingDirection, dt);
            return;
        }

        MoveTo(dt, _steeringTarget, _steeringStopDistance, false);
    }

    private void UpdateSeatApproach(float dt)
    {
        if (_seatTarget == null)
        {
            _mode = NavigationMode.Idle;
            return;
        }

        if (HasReachedHorizontally(_approachPoint, SeatApproachRadius))
        {
            EnterSeatAttemptMode();
            return;
        }

        RotateBodyTowards(_approachPoint, dt);
        MoveTo(dt, _approachPoint, SeatApproachRadius, false);
        UpdateApproachProgress(dt, NavigationMode.SeatAttempt, SeatApproachRadius);
    }

    private void UpdateSeatAttempt(float dt)
    {
        if (_seatTarget == null || _viking == null || _seatTarget.ChairComponent == null)
        {
            _mode = NavigationMode.Idle;
            return;
        }

        StopMoving();
        ZeroVelocity();
        RotateTowards(_seatTarget.SeatForward, dt);

        if (Time.time < _nextAttemptTime)
        {
            return;
        }

        _nextAttemptTime = Time.time + AttemptRetryInterval;
        _seatTarget.ChairComponent.Interact(_viking, false, false);

        if (_viking.IsAttached())
        {
            _mode = NavigationMode.Seated;
        }
    }

    private void UpdateBedApproach(float dt)
    {
        if (_bedTarget == null)
        {
            _mode = NavigationMode.Idle;
            return;
        }

        if (HasReachedHorizontally(_approachPoint, BedApproachRadius))
        {
            EnterBedAttemptMode();
            return;
        }

        RotateBodyTowards(_approachPoint, dt);
        MoveTo(dt, _approachPoint, BedApproachRadius, false);
        UpdateApproachProgress(dt, NavigationMode.BedAttempt, BedApproachRadius);
    }

    private void UpdateBedAttempt(float dt)
    {
        if (_bedTarget == null || _viking == null || _bedTarget.BedComponent == null || _bedTarget.SleepAttachPoint == null)
        {
            _mode = NavigationMode.Idle;
            return;
        }

        StopMoving();
        ZeroVelocity();
        RotateTowards(_bedTarget.SleepForward, dt);

        if (Time.time < _nextAttemptTime)
        {
            return;
        }

        _nextAttemptTime = Time.time + AttemptRetryInterval;
        _bedTarget.BedComponent.Interact(_viking, false, false);

        if (_viking.IsAttached())
        {
            _mode = NavigationMode.Sleeping;
        }
    }

    private void UpdateApproachProgress(float dt, NavigationMode nextMode, float relaxedRadius)
    {
        var currentDistance = HorizontalDistanceTo(_approachPoint);
        if (currentDistance < _bestApproachDistance - ApproachProgressEpsilon)
        {
            _bestApproachDistance = currentDistance;
            _approachStuckTimer = 0f;
        }
        else
        {
            _approachStuckTimer += dt;
        }

        _approachElapsed += dt;

        if (_approachElapsed >= _approachTimeout || (_approachStuckTimer >= _approachStuckTimeout && HasReachedHorizontally(_approachPoint, relaxedRadius)))
        {
            _nextAttemptTime = 0f;
            _mode = nextMode;
            StopMoving();
            ZeroVelocity();
        }
    }

    private void EnterSeatAttemptMode()
    {
        StopMoving();
        ZeroVelocity();
        _nextAttemptTime = 0f;
        _mode = NavigationMode.SeatAttempt;
    }

    private void EnterBedAttemptMode()
    {
        StopMoving();
        ZeroVelocity();
        _nextAttemptTime = 0f;
        _mode = NavigationMode.BedAttempt;
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

    private void RotateTowards(Vector3 facingDirection, float dt)
    {
        var flatDirection = facingDirection;
        flatDirection.y = 0f;

        if (flatDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        var targetRotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, 360f * dt);
    }

    private bool HasReachedHorizontally(Vector3 targetPoint, float radius)
    {
        return HorizontalDistanceTo(targetPoint) <= radius;
    }

    private float HorizontalDistanceTo(Vector3 targetPoint)
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
