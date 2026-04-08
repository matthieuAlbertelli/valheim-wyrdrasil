using System.Collections.Generic;
using UnityEngine;

namespace Wyrdrasil.Settlements.Tool;


public sealed class RegisteredSeatData
{
    private const float NearDepth = 1.15f;
    private const float FarDepth = 1.65f;
    private const float NearSideOffset = 0.55f;
    private const float FarSideOffset = 0.85f;

    private GameObject? _furnitureRoot;
    private Chair? _chairComponent;
    private Vector3 _seatPositionSnapshot;
    private Vector3 _seatForwardSnapshot;

    public int Id { get; }
    public int BuildingId { get; }
    public int? ZoneId { get; }
    public SeatUsageType UsageType { get; }
    public string DisplayName { get; }
    public string PersistentFurnitureId { get; }

    public GameObject? FurnitureRoot => _furnitureRoot;
    public Chair? ChairComponent => _chairComponent;
    public bool HasRuntimeBinding => _furnitureRoot != null && _chairComponent != null;

    public Vector3 SeatPosition
    {
        get
        {
            var attachPoint = GetAttachPoint();
            if (attachPoint != null)
            {
                _seatPositionSnapshot = attachPoint.position;
            }

            return _seatPositionSnapshot;
        }
    }

    public Vector3 SeatForward
    {
        get
        {
            var attachPoint = GetAttachPoint();
            if (attachPoint != null)
            {
                var runtimeForward = attachPoint.forward;
                if (runtimeForward.sqrMagnitude > 0.0001f)
                {
                    _seatForwardSnapshot = runtimeForward.normalized;
                }
            }

            if (_seatForwardSnapshot.sqrMagnitude <= 0.0001f)
            {
                return Vector3.forward;
            }

            return _seatForwardSnapshot.normalized;
        }
    }

    public Vector3 SeatRight
    {
        get
        {
            var attachPoint = GetAttachPoint();
            if (attachPoint != null)
            {
                var runtimeRight = attachPoint.right;
                runtimeRight.y = 0f;
                if (runtimeRight.sqrMagnitude > 0.0001f)
                {
                    return runtimeRight.normalized;
                }
            }

            var fallbackRight = Vector3.Cross(Vector3.up, SeatForward);
            return fallbackRight.sqrMagnitude > 0.0001f ? fallbackRight.normalized : Vector3.right;
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
        string persistentFurnitureId,
        GameObject furnitureRoot,
        Chair chairComponent)
    {
        Id = id;
        BuildingId = buildingId;
        ZoneId = zoneId;
        UsageType = usageType;
        DisplayName = displayName;
        PersistentFurnitureId = persistentFurnitureId;
        UpdateRuntimeBinding(furnitureRoot, chairComponent);
    }

    public RegisteredSeatData(
        int id,
        int buildingId,
        int? zoneId,
        SeatUsageType usageType,
        string displayName,
        string persistentFurnitureId,
        Vector3 seatPosition,
        Vector3 seatForward)
    {
        Id = id;
        BuildingId = buildingId;
        ZoneId = zoneId;
        UsageType = usageType;
        DisplayName = displayName;
        PersistentFurnitureId = persistentFurnitureId;
        _furnitureRoot = null;
        _chairComponent = null;
        _seatPositionSnapshot = seatPosition;
        _seatForwardSnapshot = seatForward.sqrMagnitude > 0.0001f ? seatForward.normalized : Vector3.forward;
    }

    public void UpdateRuntimeBinding(GameObject furnitureRoot, Chair chairComponent)
    {
        _furnitureRoot = furnitureRoot;
        _chairComponent = chairComponent;

        var attachPoint = GetAttachPoint();
        if (attachPoint != null)
        {
            _seatPositionSnapshot = attachPoint.position;

            var runtimeForward = attachPoint.forward;
            _seatForwardSnapshot = runtimeForward.sqrMagnitude > 0.0001f
                ? runtimeForward.normalized
                : furnitureRoot.transform.forward.normalized;
        }
        else
        {
            _seatPositionSnapshot = furnitureRoot.transform.position;
            var forward = furnitureRoot.transform.forward;
            _seatForwardSnapshot = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        }
    }

    public void ClearRuntimeBinding()
    {
        _furnitureRoot = null;
        _chairComponent = null;
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

    private Transform? GetAttachPoint()
    {
        if (_chairComponent == null)
        {
            return null;
        }

        return _chairComponent.m_attachPoint != null ? _chairComponent.m_attachPoint : _chairComponent.transform;
    }
}
