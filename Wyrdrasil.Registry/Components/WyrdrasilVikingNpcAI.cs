using UnityEngine;
using Wyrdrasil.Registry.Diagnostics;
using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Components;

public sealed class WyrdrasilVikingNpcAI : MonsterAI
{
    private const float SeatApproachRadius = 0.95f;
    private const float SeatRetryInterval = 0.25f;
    private const float SeatApproachProgressEpsilon = 0.10f;
    private const float SeatApproachTimeoutDirect = 1.35f;
    private const float SeatApproachTimeoutFromRoute = 0.75f;
    private const float SeatApproachStuckTimeoutDirect = 0.90f;
    private const float SeatApproachStuckTimeoutFromRoute = 0.50f;

    private enum NavigationMode
    {
        Idle,
        Steering,
        SeatApproach,
        SeatAttempt,
        Seated
    }

    private WyrdrasilVikingNpc? _viking;
    private Rigidbody? _rigidbody;

    private Vector3 _steeringTarget;
    private float _steeringStopDistance;
    private Vector3 _steeringFacingDirection;
    private bool _hasSteeringTarget;

    private RegisteredSeatData? _seatTarget;
    private Vector3 _seatApproachPoint;
    private float _seatApproachElapsed;
    private float _seatApproachTimeout;
    private float _seatApproachStuckTimer;
    private float _seatApproachStuckTimeout;
    private float _bestSeatApproachDistance;
    private float _nextSeatAttemptTime;
    private NavigationMode _mode = NavigationMode.Idle;

    protected override void Awake()
    {
        base.Awake();
        _viking = GetComponent<WyrdrasilVikingNpc>();
        _rigidbody = GetComponent<Rigidbody>();
    }

    public void SetSteeringTarget(Vector3 targetPosition, float stopDistance, Vector3 facingDirection)
    {
        _steeringTarget = targetPosition;
        _steeringStopDistance = stopDistance;
        _steeringFacingDirection = facingDirection.sqrMagnitude > 0.0001f
            ? facingDirection.normalized
            : transform.forward;

        _seatTarget = null;
        _hasSteeringTarget = true;
        _mode = NavigationMode.Steering;
        enabled = true;
    }

    public void ClearSteering()
    {
        _hasSteeringTarget = false;
        _seatTarget = null;
        _mode = _viking != null && _viking.IsAttached() ? NavigationMode.Seated : NavigationMode.Idle;
        StopMoving();
        ZeroVelocity();
    }

    public void StartSeatApproach(RegisteredSeatData seat, bool arrivedFromWaypointRoute = false)
    {
        _seatTarget = seat;
        _seatApproachPoint = seat.ApproachPosition;
        _seatApproachElapsed = 0f;
        _seatApproachTimeout = arrivedFromWaypointRoute ? SeatApproachTimeoutFromRoute : SeatApproachTimeoutDirect;
        _seatApproachStuckTimeout = arrivedFromWaypointRoute ? SeatApproachStuckTimeoutFromRoute : SeatApproachStuckTimeoutDirect;
        _seatApproachStuckTimer = 0f;
        _bestSeatApproachDistance = float.MaxValue;
        _nextSeatAttemptTime = 0f;
        _hasSteeringTarget = false;
        _mode = NavigationMode.SeatApproach;
        enabled = true;

        WyrdrasilSeatDebug.Log(this,
            $"StartSeatApproach seatId={seat.Id} route={(arrivedFromWaypointRoute ? "yes" : "no")} approach={_seatApproachPoint} timeout={_seatApproachTimeout:0.00}");
    }

    public override bool UpdateAI(float dt)
    {
        if (_viking == null)
        {
            return true;
        }

        if (_viking.IsAttached())
        {
            _mode = NavigationMode.Seated;
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

            case NavigationMode.Seated:
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

        RotateBodyTowards(_steeringTarget, dt);
        MoveTo(dt, _steeringTarget, _steeringStopDistance, false);
    }

    private void UpdateSeatApproach(float dt)
    {
        if (_seatTarget == null)
        {
            _mode = NavigationMode.Idle;
            return;
        }

        if (HasReachedHorizontally(_seatApproachPoint, SeatApproachRadius))
        {
            WyrdrasilSeatDebug.Log(this,
                $"Reached seat approach zone seatId={_seatTarget.Id} dist={HorizontalDistanceTo(_seatApproachPoint):0.00}");
            EnterSeatAttemptMode();
            return;
        }

        RotateBodyTowards(_seatApproachPoint, dt);
        MoveTo(dt, _seatApproachPoint, SeatApproachRadius, false);

        _seatApproachElapsed += dt;
        UpdateSeatApproachProgress(dt);
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

        if (Time.time < _nextSeatAttemptTime)
        {
            return;
        }

        _nextSeatAttemptTime = Time.time + SeatRetryInterval;
        WyrdrasilSeatDebug.Log(this,
            $"SeatAttempt try seatId={_seatTarget.Id} approachDist={HorizontalDistanceTo(_seatApproachPoint):0.00} seatDist={HorizontalDistanceTo(_seatTarget.SeatPosition):0.00}");

        _seatTarget.ChairComponent.Interact(_viking, false, false);

        if (_viking.IsAttached())
        {
            _mode = NavigationMode.Seated;
            WyrdrasilSeatDebug.Log(this, $"SeatAttempt success seatId={_seatTarget.Id}");
            return;
        }

        WyrdrasilSeatDebug.Log(this, $"SeatAttempt failed -> terminal retry seatId={_seatTarget.Id}");
    }

    private void UpdateSeatApproachProgress(float dt)
    {
        var currentDistance = HorizontalDistanceTo(_seatApproachPoint);
        if (currentDistance < _bestSeatApproachDistance - SeatApproachProgressEpsilon)
        {
            _bestSeatApproachDistance = currentDistance;
            _seatApproachStuckTimer = 0f;
        }
        else
        {
            _seatApproachStuckTimer += dt;
        }

        if (_seatApproachElapsed >= _seatApproachTimeout)
        {
            WyrdrasilSeatDebug.Log(this,
                $"SeatApproach timeout -> forcing terminal seat attempt seatId={_seatTarget?.Id ?? 0} elapsed={_seatApproachElapsed:0.00}");
            EnterSeatAttemptMode();
            return;
        }

        if (_seatApproachStuckTimer >= _seatApproachStuckTimeout)
        {
            WyrdrasilSeatDebug.Log(this,
                $"SeatApproach stuck -> forcing terminal seat attempt seatId={_seatTarget?.Id ?? 0} seatDist={(_seatTarget != null ? HorizontalDistanceTo(_seatTarget.SeatPosition) : 0f):0.00}");
            EnterSeatAttemptMode();
        }
    }

    private void EnterSeatAttemptMode()
    {
        StopMoving();
        ZeroVelocity();
        _nextSeatAttemptTime = 0f;
        _mode = NavigationMode.SeatAttempt;
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
