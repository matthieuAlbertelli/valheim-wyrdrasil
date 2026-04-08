using System.Collections.Generic;
using UnityEngine;

namespace Wyrdrasil.Settlements.Tool;


public sealed class RegisteredBedData
{
    private const float ApproachDepth = 1.05f;

    private GameObject? _furnitureRoot;
    private Bed? _bedComponent;
    private Transform? _sleepAttachPoint;
    private Vector3 _sleepPositionSnapshot;
    private Vector3 _sleepForwardSnapshot;

    public int Id { get; }
    public int BuildingId { get; }
    public int ZoneId { get; }
    public string DisplayName { get; }
    public string PersistentFurnitureId { get; }

    public GameObject? FurnitureRoot => _furnitureRoot;
    public Bed? BedComponent => _bedComponent;
    public Transform? SleepAttachPoint => _sleepAttachPoint;
    public bool HasRuntimeBinding => _furnitureRoot != null && _bedComponent != null && _sleepAttachPoint != null;

    public Vector3 SleepPosition
    {
        get
        {
            if (_sleepAttachPoint != null)
            {
                _sleepPositionSnapshot = _sleepAttachPoint.position;
            }

            return _sleepPositionSnapshot;
        }
    }

    public Vector3 SleepForward
    {
        get
        {
            if (_sleepAttachPoint != null)
            {
                var runtimeForward = _sleepAttachPoint.forward;
                runtimeForward.y = 0f;
                if (runtimeForward.sqrMagnitude > 0.0001f)
                {
                    _sleepForwardSnapshot = runtimeForward.normalized;
                }
            }

            return _sleepForwardSnapshot.sqrMagnitude > 0.0001f
                ? _sleepForwardSnapshot.normalized
                : Vector3.forward;
        }
    }

    public Vector3 ApproachPosition => SleepPosition - (SleepForward * ApproachDepth);
    public int? AssignedRegisteredNpcId { get; private set; }

    public RegisteredBedData(
        int id,
        int buildingId,
        int zoneId,
        string displayName,
        string persistentFurnitureId,
        GameObject furnitureRoot,
        Bed bedComponent)
    {
        Id = id;
        BuildingId = buildingId;
        ZoneId = zoneId;
        DisplayName = displayName;
        PersistentFurnitureId = persistentFurnitureId;
        UpdateRuntimeBinding(furnitureRoot, bedComponent);
    }

    public RegisteredBedData(
        int id,
        int buildingId,
        int zoneId,
        string displayName,
        string persistentFurnitureId,
        Vector3 sleepPosition,
        Vector3 sleepForward)
    {
        Id = id;
        BuildingId = buildingId;
        ZoneId = zoneId;
        DisplayName = displayName;
        PersistentFurnitureId = persistentFurnitureId;
        _sleepPositionSnapshot = sleepPosition;
        _sleepForwardSnapshot = sleepForward.sqrMagnitude > 0.0001f ? sleepForward.normalized : Vector3.forward;
    }

    public void UpdateRuntimeBinding(GameObject furnitureRoot, Bed bedComponent)
    {
        _furnitureRoot = furnitureRoot;
        _bedComponent = bedComponent;
        _sleepAttachPoint = EnsureAttachPoint(furnitureRoot);
        _sleepPositionSnapshot = _sleepAttachPoint.position;

        var runtimeForward = _sleepAttachPoint.forward;
        runtimeForward.y = 0f;
        _sleepForwardSnapshot = runtimeForward.sqrMagnitude > 0.0001f
            ? runtimeForward.normalized
            : furnitureRoot.transform.forward.normalized;
    }

    public void ClearRuntimeBinding()
    {
        _furnitureRoot = null;
        _bedComponent = null;
        _sleepAttachPoint = null;
    }

    public void AssignRegisteredNpc(int registeredNpcId)
    {
        AssignedRegisteredNpcId = registeredNpcId;
    }

    public void ClearAssignedRegisteredNpc()
    {
        AssignedRegisteredNpcId = null;
    }

    private static Transform EnsureAttachPoint(GameObject furnitureRoot)
    {
        const string attachPointName = "Wyrdrasil_BedAttachPoint";
        var existing = furnitureRoot.transform.Find(attachPointName);
        if (existing == null)
        {
            var child = new GameObject(attachPointName);
            existing = child.transform;
            existing.SetParent(furnitureRoot.transform, false);
        }

        var surfacePosition = ComputeSleepPosition(furnitureRoot);
        var forward = furnitureRoot.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = Vector3.forward;
        }

        existing.position = surfacePosition;
        existing.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        return existing;
    }

    private static Vector3 ComputeSleepPosition(GameObject furnitureRoot)
    {
        var renderers = furnitureRoot.GetComponentsInChildren<Renderer>(true);
        var hasBounds = false;
        var combinedBounds = new Bounds(furnitureRoot.transform.position, Vector3.zero);

        foreach (var renderer in renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                combinedBounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
        {
            return furnitureRoot.transform.position + Vector3.up * 0.35f;
        }

        return new Vector3(
            combinedBounds.center.x,
            combinedBounds.center.y + Mathf.Max(0.15f, combinedBounds.extents.y * 0.25f),
            combinedBounds.center.z);
    }
}
