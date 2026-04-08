using System;
using System.Collections.Generic;
using System.Linq;

namespace Wyrdrasil.Souls.Tool;


public sealed class RegisteredNpcData
{
    private readonly List<ResidentScheduleEntryData> _scheduleEntries = new();

    public int Id { get; }
    public string DisplayName { get; }
    public VikingIdentityData Identity { get; }
    public NpcRole Role { get; private set; } = NpcRole.Villager;
    public int? AssignedSlotId { get; private set; }
    public int? AssignedSeatId { get; private set; }
    public int? AssignedBedId { get; private set; }
    public ResidentPresenceSnapshotData PresenceSnapshot { get; } = new();
    public IReadOnlyList<ResidentScheduleEntryData> ScheduleEntries => _scheduleEntries;

    public RegisteredNpcData(int id, string displayName, VikingIdentityData identity)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Registered NPC display name cannot be null or whitespace.", nameof(displayName));
        }

        DisplayName = displayName;
        Identity = identity ?? throw new ArgumentNullException(nameof(identity));
        Id = id;
    }

    public void SetRole(NpcRole role) => Role = role;
    public void AssignSlot(int slotId) => AssignedSlotId = slotId;
    public void ClearAssignedSlot() => AssignedSlotId = null;
    public void AssignSeat(int seatId) => AssignedSeatId = seatId;
    public void ClearAssignedSeat() => AssignedSeatId = null;
    public void AssignBed(int bedId) => AssignedBedId = bedId;
    public void ClearAssignedBed() => AssignedBedId = null;

    public void SetScheduleEntries(IEnumerable<ResidentScheduleEntryData> entries)
    {
        _scheduleEntries.Clear();
        if (entries == null)
        {
            return;
        }

        _scheduleEntries.AddRange(entries.Where(entry => entry != null));
    }

    public void ReplaceScheduleEntries(ResidentRoutineActivityType activityType, IEnumerable<ResidentScheduleEntryData> entries)
    {
        _scheduleEntries.RemoveAll(entry => entry.ActivityType == activityType);
        if (entries == null)
        {
            return;
        }

        _scheduleEntries.AddRange(entries.Where(entry => entry != null));
    }

    public void RemoveScheduleEntries(ResidentRoutineActivityType activityType)
    {
        _scheduleEntries.RemoveAll(entry => entry.ActivityType == activityType);
    }
}
