using UnityEngine;

namespace Wyrdrasil.Souls.Tool;


public sealed class ResidentPresenceSnapshotData
{
    public ResidentRestoreMode RestoreMode { get; private set; } = ResidentRestoreMode.None;
    public Vector3 WorldPosition { get; private set; }
    public float WorldYawDegrees { get; private set; }

    public bool ShouldRespawnOnLoad => RestoreMode != ResidentRestoreMode.None;

    public void Clear()
    {
        RestoreMode = ResidentRestoreMode.None;
        WorldPosition = Vector3.zero;
        WorldYawDegrees = 0f;
    }

    public void SetWorldPosition(Vector3 worldPosition, float worldYawDegrees)
    {
        RestoreMode = ResidentRestoreMode.WorldPosition;
        WorldPosition = worldPosition;
        WorldYawDegrees = worldYawDegrees;
    }

    public void SetAssignedSlotAnchor(Vector3 worldPosition, float worldYawDegrees)
    {
        RestoreMode = ResidentRestoreMode.AssignedSlotAnchor;
        WorldPosition = worldPosition;
        WorldYawDegrees = worldYawDegrees;
    }

    public void SetAssignedSeatAnchor(Vector3 worldPosition, float worldYawDegrees)
    {
        RestoreMode = ResidentRestoreMode.AssignedSeatAnchor;
        WorldPosition = worldPosition;
        WorldYawDegrees = worldYawDegrees;
    }

    public void SetAssignedBedAnchor(Vector3 worldPosition, float worldYawDegrees)
    {
        RestoreMode = ResidentRestoreMode.AssignedBedAnchor;
        WorldPosition = worldPosition;
        WorldYawDegrees = worldYawDegrees;
    }
}
