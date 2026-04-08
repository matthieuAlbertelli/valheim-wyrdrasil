using UnityEngine;

namespace Wyrdrasil.Settlements.Tool;


public sealed class ZoneSlotData
{
    public int Id { get; }

    public int BuildingId { get; }

    public int ZoneId { get; }

    public ZoneSlotType SlotType { get; }

    public Vector3 Position { get; }

    public int? AssignedRegisteredNpcId { get; private set; }

    public ZoneSlotData(int id, int buildingId, int zoneId, ZoneSlotType slotType, Vector3 position)
    {
        Id = id;
        BuildingId = buildingId;
        ZoneId = zoneId;
        SlotType = slotType;
        Position = position;
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
