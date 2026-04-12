using UnityEngine;

namespace Wyrdrasil.Routines.Tool;

public readonly struct DockingRequest
{
    public Vector3 TargetPosition { get; }
    public Vector3 TargetForward { get; }
    public float PositionTolerance { get; }
    public float AngleToleranceDegrees { get; }
    public float MaxDurationSeconds { get; }

    public DockingRequest(
        Vector3 targetPosition,
        Vector3 targetForward,
        float positionTolerance,
        float angleToleranceDegrees,
        float maxDurationSeconds)
    {
        TargetPosition = targetPosition;
        TargetForward = targetForward;
        PositionTolerance = Mathf.Max(0.01f, positionTolerance);
        AngleToleranceDegrees = Mathf.Max(0.1f, angleToleranceDegrees);
        MaxDurationSeconds = Mathf.Max(0.1f, maxDurationSeconds);
    }
}
