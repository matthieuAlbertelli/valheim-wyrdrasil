using UnityEngine;
using Wyrdrasil.Registry.Diagnostics;
using Wyrdrasil.Souls.Components;

namespace Wyrdrasil.Routines.Components;

public sealed class WyrdrasilEngagedPoseController : MonoBehaviour
{
    private Rigidbody? _rigidbody;
    private WyrdrasilVikingNpcAI? _npcAi;
    private bool _isEngaged;
    private Vector3 _targetPosition;
    private Quaternion _targetRotation = Quaternion.identity;
    private int _lastLoggedFrame = -1;

    public bool IsEngaged => _isEngaged;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _npcAi = GetComponent<WyrdrasilVikingNpcAI>();
        enabled = false;
    }

    public void Engage(Vector3 targetPosition, Vector3 targetForward)
    {
        _targetPosition = targetPosition;

        targetForward.y = 0f;
        if (targetForward.sqrMagnitude <= 0.0001f)
        {
            targetForward = transform.forward;
            targetForward.y = 0f;
        }

        targetForward.Normalize();
        _targetRotation = Quaternion.LookRotation(targetForward, Vector3.up);
        _isEngaged = true;
        enabled = true;

        _npcAi?.EnterRegistryTravelLock();
        _npcAi?.SetCivilianWalkLocomotion(false);
        ApplyPose();
        LogState("Engage");
    }

    public void Disengage()
    {
        if (!_isEngaged)
        {
            return;
        }

        _isEngaged = false;
        enabled = false;
        ZeroVelocity();
        _npcAi?.ExitRegistryTravelLock();
        _npcAi?.SetCivilianWalkLocomotion(true);
        LogState("Disengage");
    }

    private void Update()
    {
        ApplyPose();
    }

    private void FixedUpdate()
    {
        ApplyPose();
    }

    private void LateUpdate()
    {
        ApplyPose();
    }

    private void ApplyPose()
    {
        if (!_isEngaged)
        {
            return;
        }

        transform.SetPositionAndRotation(_targetPosition, _targetRotation);

        if (_rigidbody != null)
        {
            _rigidbody.position = _targetPosition;
            _rigidbody.rotation = _targetRotation;
        }

        _npcAi?.ClearSteering();
        ZeroVelocity();

        if (Time.frameCount != _lastLoggedFrame && Time.frameCount % 30 == 0)
        {
            _lastLoggedFrame = Time.frameCount;
            LogState("Hold");
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

    private void LogState(string phase)
    {
        var forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude > 0.0001f)
        {
            forward.Normalize();
        }

        WyrdrasilOccupationDebug.LogCraftStation(
            GetComponent<Character>(),
            $"EngagedPoseController {phase} position={transform.position} forward=({forward.x:0.00},{forward.y:0.00},{forward.z:0.00}) engaged={_isEngaged}");
    }
}
