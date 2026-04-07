using System;

namespace Wyrdrasil.Registry.Tool;

[Serializable]
public sealed class RegisteredNpcSaveData
{
    public int Id;
    public string DisplayName = string.Empty;
    public NpcRole Role;
    public int? AssignedSlotId;
    public int? AssignedSeatId;
    public VikingIdentitySaveData Identity = new();
    public ResidentPresenceSnapshotSaveData PresenceSnapshot = new();
}
