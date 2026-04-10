using System;
using Wyrdrasil.Core.Tool;

namespace Wyrdrasil.Souls.Tool;

[Serializable]
public sealed class ResidentPresenceSnapshotSaveData
{
    public ResidentRestoreMode RestoreMode;
    public Float3SaveData WorldPosition = new();
    public float WorldYawDegrees;
    public bool HasAssignedPurpose;
    public ResidentAssignmentPurpose AssignedPurpose;
}
