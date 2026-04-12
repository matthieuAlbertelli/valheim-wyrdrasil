using UnityEngine;

namespace Wyrdrasil.Settlements.Tool;


public sealed class ZoneSlotData
{
    public int Id { get; }

    public int BuildingId { get; }

    public int ZoneId { get; }

    public ZoneSlotType SlotType { get; }

    public Vector3 Position { get; }

    public Vector3 FacingDirection { get; }

    public int? AssignedRegisteredNpcId { get; private set; }

    public ZoneSlotData(int id, int buildingId, int zoneId, ZoneSlotType slotType, Vector3 position, Vector3 facingDirection)
    {
        Id = id;
        BuildingId = buildingId;
        ZoneId = zoneId;
        SlotType = slotType;
        Position = position;

        facingDirection.y = 0f;
        FacingDirection = facingDirection.sqrMagnitude > 0.0001f ? facingDirection.normalized : Vector3.forward;
    }

    public void AssignRegisteredNpc(int registeredNpcId)
    {
        AssignedRegisteredNpcId = registeredNpcId;
    }

    public void ClearAssignedRegisteredNpc()
    {
        AssignedRegisteredNpcId = null;
    }
}
