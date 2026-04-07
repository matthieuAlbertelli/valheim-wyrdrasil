using System;

namespace Wyrdrasil.Registry.Tool;

public sealed class RegisteredNpcData
{
    public int Id { get; }
    public string DisplayName { get; }
    public VikingIdentityData Identity { get; }
    public NpcRole Role { get; private set; } = NpcRole.Villager;
    public int? AssignedSlotId { get; private set; }
    public int? AssignedSeatId { get; private set; }
    public ResidentPresenceSnapshotData PresenceSnapshot { get; } = new();

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
}
