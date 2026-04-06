using UnityEngine;

namespace Wyrdrasil.Registry.Tool;

public sealed class RegisteredSeatData
{
    private const float DefaultApproachOffset = 0.45f;

    public int Id { get; }
    public int ZoneId { get; }
    public string DisplayName { get; }
    public GameObject FurnitureRoot { get; }
    public Chair ChairComponent { get; }
    public Transform AttachPoint => ChairComponent.m_attachPoint != null ? ChairComponent.m_attachPoint : ChairComponent.transform;
    public Vector3 SeatPosition => AttachPoint.position;
    public Vector3 SeatForward => AttachPoint.forward.sqrMagnitude > 0.0001f ? AttachPoint.forward.normalized : FurnitureRoot.transform.forward;
    public Vector3 ApproachPosition => SeatPosition - (SeatForward * DefaultApproachOffset);
    public int? AssignedRegisteredNpcId { get; private set; }

    public RegisteredSeatData(int id, int zoneId, string displayName, GameObject furnitureRoot, Chair chairComponent)
    {
        Id = id;
        ZoneId = zoneId;
        DisplayName = displayName;
        FurnitureRoot = furnitureRoot;
        ChairComponent = chairComponent;
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
