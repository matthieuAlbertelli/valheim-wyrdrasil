using System;
using System.Collections.Generic;
using System.Linq;
using Wyrdrasil.Core.Tool;

namespace Wyrdrasil.Souls.Tool;

public sealed class RegisteredNpcData
{
    private readonly List<ResidentScheduleEntryData> _scheduleEntries = new();
    private readonly List<ResidentAssignmentData> _assignments = new();

    public int Id { get; }
    public string DisplayName { get; }
    public VikingIdentityData Identity { get; }
    public NpcRole Role { get; private set; } = NpcRole.Villager;
    public int? AssignedSlotId => TryGetAssignedTargetId(ResidentAssignmentPurpose.Work, OccupationTargetKind.Slot, out var targetId)
        ? targetId
        : (int?)null;
    public int? AssignedSeatId => TryGetAssignedTargetId(ResidentAssignmentPurpose.Meal, OccupationTargetKind.Seat, out var targetId)
        ? targetId
        : (int?)null;
    public int? AssignedBedId => TryGetAssignedTargetId(ResidentAssignmentPurpose.Sleep, OccupationTargetKind.Bed, out var targetId)
        ? targetId
        : (int?)null;
    public ResidentPresenceSnapshotData PresenceSnapshot { get; } = new();
    public IReadOnlyList<ResidentScheduleEntryData> ScheduleEntries => _scheduleEntries;
    public IReadOnlyList<ResidentAssignmentData> Assignments => _assignments;

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

    public bool TryGetAssignment(ResidentAssignmentPurpose purpose, out ResidentAssignmentData assignment)
    {
        foreach (var candidate in _assignments)
        {
            if (candidate.Purpose == purpose)
            {
                assignment = candidate;
                return true;
            }
        }

        assignment = default;
        return false;
    }

    public bool TryGetAssignedTarget(ResidentAssignmentPurpose purpose, out OccupationTargetRef target)
    {
        if (TryGetAssignment(purpose, out var assignment))
        {
            target = assignment.Target;
            return true;
        }

        target = default;
        return false;
    }

    public bool TryGetAssignedTargetId(ResidentAssignmentPurpose purpose, OccupationTargetKind expectedTargetKind, out int targetId)
    {
        if (TryGetAssignedTarget(purpose, out var target) && target.TargetKind == expectedTargetKind)
        {
            targetId = target.TargetId;
            return true;
        }

        targetId = default;
        return false;
    }

    public void SetAssignment(ResidentAssignmentPurpose purpose, OccupationTargetRef target)
    {
        var updatedAssignment = new ResidentAssignmentData(purpose, target);
        for (var i = 0; i < _assignments.Count; i++)
        {
            if (_assignments[i].Purpose != purpose)
            {
                continue;
            }

            _assignments[i] = updatedAssignment;
            return;
        }

        _assignments.Add(updatedAssignment);
    }

    public void ClearAssignment(ResidentAssignmentPurpose purpose)
    {
        _assignments.RemoveAll(candidate => candidate.Purpose == purpose);
    }

    public void SetAssignments(IEnumerable<ResidentAssignmentData> assignments)
    {
        _assignments.Clear();
        if (assignments == null)
        {
            return;
        }

        foreach (var assignment in assignments)
        {
            SetAssignment(assignment.Purpose, assignment.Target);
        }
    }

    public void AssignSlot(int slotId) => SetAssignment(ResidentAssignmentPurpose.Work, new OccupationTargetRef(OccupationTargetKind.Slot, slotId));
    public void ClearAssignedSlot() => ClearAssignment(ResidentAssignmentPurpose.Work);
    public void AssignSeat(int seatId) => SetAssignment(ResidentAssignmentPurpose.Meal, new OccupationTargetRef(OccupationTargetKind.Seat, seatId));
    public void ClearAssignedSeat() => ClearAssignment(ResidentAssignmentPurpose.Meal);
    public void AssignBed(int bedId) => SetAssignment(ResidentAssignmentPurpose.Sleep, new OccupationTargetRef(OccupationTargetKind.Bed, bedId));
    public void ClearAssignedBed() => ClearAssignment(ResidentAssignmentPurpose.Sleep);

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
