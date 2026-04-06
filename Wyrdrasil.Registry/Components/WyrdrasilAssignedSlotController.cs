using System.Collections.Generic;
using UnityEngine;
using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Components;

public sealed class WyrdrasilAssignedSlotController : MonoBehaviour
{
    private const float HoldHorizontalLeashDistance = 0.65f;
    private const float HoldVerticalLeashDistance = 0.35f;
    private const float VerticalArrivalTolerance = 0.2f;

    private Rigidbody? _rigidbody;
    private BaseAI? _baseAi;
    private MonsterAI? _monsterAi;
    private Animator? _animator;
    private Humanoid? _humanoid;

    private readonly List<Vector3> _routePoints = new();

    private float _moveSpeed;
    private float _horizontalStopDistance;
    private int _currentRouteIndex;
    private bool _isConfigured;
    private bool _shouldAttemptSeatInteraction;
    private bool _useNativeChairInteraction;
    private bool _seatFallbackPoseActive;
    private Vector3 _finalFacingDirection;
    private Vector3 _seatApproachPosition;
    private Vector3 _seatSnapPosition;
    private Chair? _targetChair;

    private AssignedSlotOccupationPhase _phase = AssignedSlotOccupationPhase.None;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _baseAi = GetComponent<BaseAI>();
        _monsterAi = GetComponent<MonsterAI>();
        _animator = GetComponentInChildren<Animator>();
        _humanoid = GetComponent<Humanoid>();
    }

    public void ReleaseControl()
    {
        _routePoints.Clear();
        _currentRouteIndex = 0;
        _isConfigured = false;
        _shouldAttemptSeatInteraction = false;
        _useNativeChairInteraction = false;
        _seatFallbackPoseActive = false;
        _seatApproachPosition = Vector3.zero;
        _seatSnapPosition = Vector3.zero;
        _targetChair = null;
        _phase = AssignedSlotOccupationPhase.None;

        if (_baseAi != null)
        {
            _baseAi.enabled = true;
        }

        enabled = false;
    }

    public void ConfigureForRoute(
        IReadOnlyList<Vector3> waypointPositions,
        Vector3 slotPosition,
        float moveSpeed,
        float horizontalStopDistance)
    {
        enabled = true;
        DetachIfNeeded();
        ClearSeatTarget();
        ConfigureInternal(waypointPositions, slotPosition, moveSpeed, horizontalStopDistance, false, transform.forward);
    }

    public void ConfigureForDirectMovement(Vector3 slotPosition, float moveSpeed, float horizontalStopDistance)
    {
        enabled = true;
        DetachIfNeeded();
        ClearSeatTarget();

        _routePoints.Clear();
        _routePoints.Add(slotPosition);
        _moveSpeed = moveSpeed;
        _horizontalStopDistance = horizontalStopDistance;
        _currentRouteIndex = 0;
        _shouldAttemptSeatInteraction = false;
        _finalFacingDirection = transform.forward;
        _phase = AssignedSlotOccupationPhase.DirectFallback;
        _isConfigured = true;
        _seatFallbackPoseActive = false;

        StopNativeControllers();
    }

    public void ConfigureForSeatRoute(
        IReadOnlyList<Vector3> waypointPositions,
        Vector3 approachPosition,
        Vector3 seatSnapPosition,
        Vector3 seatForward,
        Chair? chair,
        float moveSpeed,
        float horizontalStopDistance)
    {
        enabled = true;
        DetachIfNeeded();
        ConfigureSeatTarget(approachPosition, seatSnapPosition, seatForward, chair);
        ConfigureInternal(waypointPositions, approachPosition, moveSpeed, horizontalStopDistance, true, seatForward);
    }

    public void ConfigureForDirectSeatMovement(
        Vector3 approachPosition,
        Vector3 seatSnapPosition,
        Vector3 seatForward,
        Chair? chair,
        float moveSpeed,
        float horizontalStopDistance)
    {
        enabled = true;
        DetachIfNeeded();
        ConfigureSeatTarget(approachPosition, seatSnapPosition, seatForward, chair);

        _routePoints.Clear();
        _routePoints.Add(approachPosition);
        _moveSpeed = moveSpeed;
        _horizontalStopDistance = horizontalStopDistance;
        _currentRouteIndex = 0;
        _shouldAttemptSeatInteraction = true;
        _finalFacingDirection = seatForward.sqrMagnitude > 0.0001f ? seatForward.normalized : transform.forward;
        _phase = AssignedSlotOccupationPhase.DirectFallback;
        _isConfigured = true;
        _seatFallbackPoseActive = false;

        StopNativeControllers();
    }

    private void ConfigureSeatTarget(Vector3 approachPosition, Vector3 seatSnapPosition, Vector3 seatForward, Chair? chair)
    {
        _seatApproachPosition = approachPosition;
        _seatSnapPosition = seatSnapPosition;
        _finalFacingDirection = seatForward.sqrMagnitude > 0.0001f ? seatForward.normalized : transform.forward;
        _targetChair = chair;
        _useNativeChairInteraction = chair != null && chair.m_attachPoint != null && _humanoid != null;
        _seatFallbackPoseActive = false;
    }

    private void ClearSeatTarget()
    {
        _seatApproachPosition = Vector3.zero;
        _seatSnapPosition = Vector3.zero;
        _targetChair = null;
        _useNativeChairInteraction = false;
        _seatFallbackPoseActive = false;
    }

    private void ConfigureInternal(
        IReadOnlyList<Vector3> waypointPositions,
        Vector3 finalPosition,
        float moveSpeed,
        float horizontalStopDistance,
        bool shouldAttemptSeatInteraction,
        Vector3 finalFacingDirection)
    {
        _routePoints.Clear();
        foreach (var waypointPosition in waypointPositions)
        {
            _routePoints.Add(waypointPosition);
        }

        _routePoints.Add(finalPosition);
        _moveSpeed = moveSpeed;
        _horizontalStopDistance = horizontalStopDistance;
        _currentRouteIndex = 0;
        _shouldAttemptSeatInteraction = shouldAttemptSeatInteraction;
        _finalFacingDirection = finalFacingDirection.sqrMagnitude > 0.0001f ? finalFacingDirection.normalized : transform.forward;
        _phase = AssignedSlotOccupationPhase.TraverseRoute;
        _isConfigured = true;
        _seatFallbackPoseActive = false;

        StopNativeControllers();
    }

    private void Update()
    {
        if (!_isConfigured)
        {
            return;
        }

        switch (_phase)
        {
            case AssignedSlotOccupationPhase.TraverseRoute:
            case AssignedSlotOccupationPhase.DirectFallback:
                UpdateRouteMovement();
                break;

            case AssignedSlotOccupationPhase.Settle:
                UpdateSettle();
                break;

            case AssignedSlotOccupationPhase.Hold:
                UpdateHold();
                break;
        }
    }

    private void UpdateRouteMovement()
    {
        if (_currentRouteIndex >= _routePoints.Count)
        {
            _phase = _shouldAttemptSeatInteraction
                ? AssignedSlotOccupationPhase.Settle
                : AssignedSlotOccupationPhase.Hold;
            return;
        }

        var targetPoint = _routePoints[_currentRouteIndex];
        if (MoveTowardsPoint(targetPoint, allowRotationTowardsTarget: true))
        {
            _currentRouteIndex++;

            if (_currentRouteIndex >= _routePoints.Count)
            {
                _phase = _shouldAttemptSeatInteraction
                    ? AssignedSlotOccupationPhase.Settle
                    : AssignedSlotOccupationPhase.Hold;
            }
        }
    }

    private void UpdateSettle()
    {
        var targetPoint = _seatApproachPosition != Vector3.zero
            ? _seatApproachPosition
            : _routePoints.Count > 0
                ? _routePoints[_routePoints.Count - 1]
                : transform.position;

        var arrived = MoveTowardsPoint(targetPoint, allowRotationTowardsTarget: false);
        RotateTowardsFinalFacing();

        if (!arrived)
        {
            return;
        }

        SetWorldPosition(targetPoint);
        RotateTowardsFinalFacing(forceSnap: true);

        if (TryEnterNativeChair())
        {
            _seatFallbackPoseActive = false;
            _phase = AssignedSlotOccupationPhase.Hold;
            return;
        }

        if (_seatSnapPosition != Vector3.zero)
        {
            SnapSeatFallbackPosition();
        }

        RotateTowardsFinalFacing(forceSnap: true);
        TryEnterSeatPose();
        _seatFallbackPoseActive = true;
        _phase = AssignedSlotOccupationPhase.Hold;
    }

    private void UpdateHold()
    {
        if (_humanoid != null && _humanoid.IsAttached())
        {
            return;
        }

        if (_seatFallbackPoseActive || _shouldAttemptSeatInteraction)
        {
            SnapSeatFallbackPosition();
            return;
        }

        if (_routePoints.Count == 0)
        {
            return;
        }

        var slotPosition = _routePoints[_routePoints.Count - 1];
        var horizontalDistance = GetHorizontalDistanceToPoint(slotPosition);
        var verticalDistance = GetVerticalDistanceToPoint(slotPosition);

        if (horizontalDistance > HoldHorizontalLeashDistance || verticalDistance > HoldVerticalLeashDistance)
        {
            _routePoints.Clear();
            _routePoints.Add(slotPosition);
            _currentRouteIndex = 0;
            _phase = AssignedSlotOccupationPhase.DirectFallback;
        }
    }

    private bool TryEnterNativeChair()
    {
        if (!_useNativeChairInteraction || _targetChair == null || _humanoid == null)
        {
            return false;
        }

        if (_seatSnapPosition != Vector3.zero)
        {
            SnapSeatFallbackPosition();
        }

        RotateTowardsFinalFacing(forceSnap: true);

        if (_humanoid is WyrdrasilVikingNpc viking)
        {
            viking.AttachToChair(_targetChair);
            return viking.IsAttached();
        }

        _targetChair.Interact(_humanoid, false, false);
        return _humanoid.IsAttached();
    }

    private bool MoveTowardsPoint(Vector3 targetPoint, bool allowRotationTowardsTarget)
    {
        if (_humanoid != null && _humanoid.IsAttached())
        {
            return true;
        }

        var currentPosition = transform.position;
        var toTarget = targetPoint - currentPosition;
        var horizontalDirection = new Vector3(toTarget.x, 0f, toTarget.z);
        var horizontalDistance = horizontalDirection.magnitude;
        var verticalDistance = Mathf.Abs(targetPoint.y - currentPosition.y);

        if (horizontalDistance <= _horizontalStopDistance && verticalDistance <= VerticalArrivalTolerance)
        {
            SetWorldPosition(targetPoint);
            return true;
        }

        var nextPosition = Vector3.MoveTowards(currentPosition, targetPoint, _moveSpeed * Time.deltaTime);
        SetWorldPosition(nextPosition);

        if (allowRotationTowardsTarget && horizontalDirection.sqrMagnitude > 0.0001f)
        {
            var targetRotation = Quaternion.LookRotation(horizontalDirection.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 12f * Time.deltaTime);
        }

        return false;
    }

    private void RotateTowardsFinalFacing(bool forceSnap = false)
    {
        if (_finalFacingDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        var targetRotation = Quaternion.LookRotation(_finalFacingDirection, Vector3.up);
        transform.rotation = forceSnap
            ? targetRotation
            : Quaternion.Slerp(transform.rotation, targetRotation, 12f * Time.deltaTime);
    }

    private void TryEnterSeatPose()
    {
        if (_animator == null)
        {
            return;
        }

        var attempted = false;

        attempted |= TrySetAnimatorBool("sit", true);
        attempted |= TrySetAnimatorBool("Sit", true);
        attempted |= TrySetAnimatorBool("sitting", true);
        attempted |= TrySetAnimatorBool("Sitting", true);
        attempted |= TrySetAnimatorTrigger("sit");
        attempted |= TrySetAnimatorTrigger("Sit");

        if (!attempted)
        {
            _ =
                TryPlayAnimatorState("sit") ||
                TryPlayAnimatorState("Sit") ||
                TryPlayAnimatorState("sitting") ||
                TryPlayAnimatorState("Sitting");
        }
    }

    private bool TrySetAnimatorBool(string parameterName, bool value)
    {
        if (_animator == null)
        {
            return false;
        }

        foreach (var parameter in _animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Bool && parameter.name == parameterName)
            {
                _animator.SetBool(parameterName, value);
                return true;
            }
        }

        return false;
    }

    private bool TrySetAnimatorTrigger(string parameterName)
    {
        if (_animator == null)
        {
            return false;
        }

        foreach (var parameter in _animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == parameterName)
            {
                _animator.SetTrigger(parameterName);
                return true;
            }
        }

        return false;
    }

    private bool TryPlayAnimatorState(string stateName)
    {
        if (_animator == null || !_animator.HasState(0, Animator.StringToHash(stateName)))
        {
            return false;
        }

        _animator.Play(stateName);
        return true;
    }

    private void SetWorldPosition(Vector3 worldPosition)
    {
        transform.position = worldPosition;

        if (_rigidbody != null)
        {
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
        }
    }

    private void SnapSeatFallbackPosition()
    {
        if (_seatSnapPosition == Vector3.zero)
        {
            return;
        }

        transform.position = _seatSnapPosition;
        RotateTowardsFinalFacing(forceSnap: true);

        if (_rigidbody != null)
        {
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
        }
    }

    private float GetHorizontalDistanceToPoint(Vector3 targetPoint)
    {
        var current = transform.position;
        var delta = new Vector2(targetPoint.x - current.x, targetPoint.z - current.z);
        return delta.magnitude;
    }

    private float GetVerticalDistanceToPoint(Vector3 targetPoint)
    {
        return Mathf.Abs(targetPoint.y - transform.position.y);
    }

    private void StopNativeControllers()
    {
        if (_monsterAi != null)
        {
            _monsterAi.SetFollowTarget(null);
        }

        if (_baseAi != null)
        {
            _baseAi.enabled = false;
        }
    }

    private void DetachIfNeeded()
    {
        if (_humanoid == null || !_humanoid.IsAttached())
        {
            return;
        }

        _humanoid.AttachStop();
    }
}