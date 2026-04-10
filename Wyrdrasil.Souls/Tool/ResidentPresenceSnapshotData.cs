using UnityEngine;

namespace Wyrdrasil.Souls.Tool;

public sealed class ResidentPresenceSnapshotData
{
    public ResidentRestoreMode RestoreMode { get; private set; } = ResidentRestoreMode.None;
    public Vector3 WorldPosition { get; private set; }
    public float WorldYawDegrees { get; private set; }
    public ResidentAssignmentPurpose? AssignedPurpose { get; private set; }

    public bool ShouldRespawnOnLoad => RestoreMode != ResidentRestoreMode.None;

    public void Clear()
    {
        RestoreMode = ResidentRestoreMode.None;
        WorldPosition = Vector3.zero;
        WorldYawDegrees = 0f;
        AssignedPurpose = null;
    }

    public void SetWorldPosition(Vector3 worldPosition, float worldYawDegrees)
    {
        RestoreMode = ResidentRestoreMode.WorldPosition;
        WorldPosition = worldPosition;
        WorldYawDegrees = worldYawDegrees;
        AssignedPurpose = null;
    }

    public void SetAssignedTargetAnchor(ResidentAssignmentPurpose purpose, Vector3 worldPosition, float worldYawDegrees)
    {
        RestoreMode = ResidentRestoreMode.AssignedTargetAnchor;
        WorldPosition = worldPosition;
        WorldYawDegrees = worldYawDegrees;
        AssignedPurpose = purpose;
    }

    public bool IsAssignedTargetAnchor(ResidentAssignmentPurpose purpose)
    {
        return RestoreMode == ResidentRestoreMode.AssignedTargetAnchor && AssignedPurpose == purpose;
    }
}
