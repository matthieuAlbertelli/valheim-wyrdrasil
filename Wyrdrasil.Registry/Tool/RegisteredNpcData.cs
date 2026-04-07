using UnityEngine;

namespace Wyrdrasil.Registry.Tool;

public sealed class RegisteredNpcData
{
    public int Id { get; }
    public int CharacterInstanceId { get; }
    public string DisplayName { get; }
    public Character Character { get; }
    public VikingIdentityData? Identity { get; }
    public NpcRole Role { get; private set; } = NpcRole.Villager;
    public int? AssignedSlotId { get; private set; }
    public int? AssignedSeatId { get; private set; }

    public RegisteredNpcData(int id, int characterInstanceId, string displayName, Character character, VikingIdentityData? identity = null)
    {
        Id = id;
        CharacterInstanceId = characterInstanceId;
        DisplayName = displayName;
        Character = character;
        Identity = identity;
    }

    public void SetRole(NpcRole role) => Role = role;
    public void AssignSlot(int slotId) => AssignedSlotId = slotId;
    public void ClearAssignedSlot() => AssignedSlotId = null;
    public void AssignSeat(int seatId) => AssignedSeatId = seatId;
    public void ClearAssignedSeat() => AssignedSeatId = null;
}
