using UnityEngine;

namespace Wyrdrasil.Registry.Tool;

public sealed class RegisteredSeatData
{
    public int Id { get; }
    public int ZoneId { get; }
    public string DisplayName { get; }
    public GameObject FurnitureRoot { get; }
    public Vector3 SeatPosition => FurnitureRoot != null ? FurnitureRoot.transform.position : _fallbackPosition;
    public Vector3 SeatForward => FurnitureRoot != null ? FurnitureRoot.transform.forward : Vector3.forward;
    public int? AssignedRegisteredNpcId { get; private set; }

    private readonly Vector3 _fallbackPosition;

    public RegisteredSeatData(int id, int zoneId, string displayName, GameObject furnitureRoot)
    {
        Id = id;
        ZoneId = zoneId;
        DisplayName = displayName;
        FurnitureRoot = furnitureRoot;
        _fallbackPosition = furnitureRoot.transform.position;
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
