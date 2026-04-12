using UnityEngine;
using Wyrdrasil.Routines.Tool;
using Wyrdrasil.Souls.Components;

namespace Wyrdrasil.Routines.Components;

public sealed class WyrdrasilOccupationDockingController : MonoBehaviour
{
    private const float DockLinearSpeed = 1.65f;
    private const float DockAngularSpeedDegrees = 540f;
    private const float StableDurationSeconds = 0.15f;

    private Rigidbody? _rigidbody;
    private WyrdrasilVikingNpcAI? _npcAi;
    private DockingRequest _request;
    private DockingState _state = DockingState.Idle;
    private float _startedAtTime;
    private float _stableSinceTime;

    public DockingState State => _state;
    public bool IsActive => _state == DockingState.Active;
    public bool IsDocked => _state == DockingState.Succeeded;
    public bool HasFailed => _state == DockingState.Failed;
    public float CurrentHorizontalDistance { get; private set; }
    public float CurrentAngleErrorDegrees { get; private set; }

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _npcAi = GetComponent<WyrdrasilVikingNpcAI>();
    }

    public void BeginDocking(DockingRequest request)
    {
        _request = request;
        _state = DockingState.Active;
        _startedAtTime = Time.time;
        _stableSinceTime = 0f;
        CurrentHorizontalDistance = float.MaxValue;
        CurrentAngleErrorDegrees = 180f;

        _npcAi?.ClearSteering();
        _npcAi?.SetCivilianWalkLocomotion(true);
        ZeroVelocity();
        enabled = true;
    }

    public void CancelDocking()
    {
        if (_state == DockingState.Idle)
        {
            return;
        }

        _state = DockingState.Cancelled;
        ZeroVelocity();
        enabled = false;
    }

    private void Update()
    {
        if (_state != DockingState.Active)
        {
            return;
        }

        _npcAi?.ClearSteering();

        var dt = Time.deltaTime;
        var flatTargetForward = _request.TargetForward;
        flatTargetForward.y = 0f;
        if (flatTargetForward.sqrMagnitude <= 0.0001f)
        {
            flatTargetForward = transform.forward;
            flatTargetForward.y = 0f;
        }

        flatTargetForward.Normalize();

        var targetPosition = _request.TargetPosition;
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, DockLinearSpeed * dt);
        var targetRotation = Quaternion.LookRotation(flatTargetForward, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, DockAngularSpeedDegrees * dt);

        CurrentHorizontalDistance = GetHorizontalDistance(transform.position, targetPosition);
        CurrentAngleErrorDegrees = Quaternion.Angle(transform.rotation, targetRotation);

        if (CurrentHorizontalDistance <= _request.PositionTolerance &&
            CurrentAngleErrorDegrees <= _request.AngleToleranceDegrees)
        {
            if (_stableSinceTime <= 0f)
            {
                _stableSinceTime = Time.time;
            }
            else if (Time.time - _stableSinceTime >= StableDurationSeconds)
            {
                transform.position = targetPosition;
                transform.rotation = targetRotation;
                ZeroVelocity();
                _state = DockingState.Succeeded;
                enabled = false;
                return;
            }
        }
        else
        {
            _stableSinceTime = 0f;
        }

        if (Time.time - _startedAtTime >= _request.MaxDurationSeconds)
        {
            ZeroVelocity();
            _state = DockingState.Failed;
            enabled = false;
        }
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

    private static float GetHorizontalDistance(Vector3 a, Vector3 b)
    {
        var delta = b - a;
        delta.y = 0f;
        return delta.magnitude;
    }
}
