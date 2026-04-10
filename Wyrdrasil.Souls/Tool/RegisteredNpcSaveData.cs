using System;
using System.Collections.Generic;

namespace Wyrdrasil.Souls.Tool;


[Serializable]
public sealed class RegisteredNpcSaveData
{
    public int Id;
    public string DisplayName = string.Empty;
    public NpcRole Role;
    public int? AssignedSlotId;
    public int? AssignedSeatId;
    public int? AssignedBedId;
    public List<ResidentAssignmentSaveData> Assignments = new();
    public VikingIdentitySaveData Identity = new();
    public ResidentPresenceSnapshotSaveData PresenceSnapshot = new();
    public List<ResidentScheduleEntrySaveData> ScheduleEntries = new();
}
