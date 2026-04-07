using System.Collections.Generic;
using UnityEngine;

namespace Wyrdrasil.Registry.Tool;

public sealed class RegisteredSeatData
{
    private const float NearDepth = 1.15f;
    private const float FarDepth = 1.65f;
    private const float NearSideOffset = 0.55f;
    private const float FarSideOffset = 0.85f;

    public int Id { get; }
    public int BuildingId { get; }
    public int? ZoneId { get; }
    public SeatUsageType UsageType { get; }
    public string DisplayName { get; }
    public GameObject FurnitureRoot { get; }
    public Chair ChairComponent { get; }
    public Transform AttachPoint => ChairComponent.m_attachPoint != null ? ChairComponent.m_attachPoint : ChairComponent.transform;
    public Vector3 SeatPosition => AttachPoint.position;
    public Vector3 SeatForward => AttachPoint.forward.sqrMagnitude > 0.0001f ? AttachPoint.forward.normalized : FurnitureRoot.transform.forward;
    public Vector3 SeatRight
    {
        get
        {
            var right = AttachPoint.right;
            right.y = 0f;

            if (right.sqrMagnitude <= 0.0001f)
            {
                right = Vector3.Cross(Vector3.up, SeatForward);
            }

            return right.normalized;
        }
    }

    public Vector3 ApproachPosition => SeatPosition - (SeatForward * NearDepth);
    public int? AssignedRegisteredNpcId { get; private set; }

    public RegisteredSeatData(
        int id,
        int buildingId,
        int? zoneId,
        SeatUsageType usageType,
        string displayName,
        GameObject furnitureRoot,
        Chair chairComponent)
    {
        Id = id;
        BuildingId = buildingId;
        ZoneId = zoneId;
        UsageType = usageType;
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

    public IEnumerable<Vector3> GetApproachCandidates()
    {
        var centerNear = SeatPosition - (SeatForward * NearDepth);
        var centerFar = SeatPosition - (SeatForward * FarDepth);
        var right = SeatRight;

        yield return centerNear;
        yield return centerNear + right * NearSideOffset;
        yield return centerNear - right * NearSideOffset;

        yield return centerFar;
        yield return centerFar + right * FarSideOffset;
        yield return centerFar - right * FarSideOffset;
    }
}
