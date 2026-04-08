using System;

namespace Wyrdrasil.Registry.Tool;

[Serializable]
public sealed class ResidentPresenceSnapshotSaveData
{
    public ResidentRestoreMode RestoreMode;
    public Float3SaveData WorldPosition = new();
    public float WorldYawDegrees;
}
