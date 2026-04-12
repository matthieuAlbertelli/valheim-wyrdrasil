using UnityEngine;

namespace Wyrdrasil.Settlements.Tool;

public sealed class RegisteredCraftStationData
{
    private const float ApproachDepth = 0.85f;
    private const string UsePointName = "Wyrdrasil_CraftStationUsePoint";
    private const string ApproachPointName = "Wyrdrasil_CraftStationApproachPoint";

    private GameObject? _furnitureRoot;
    private CraftingStation? _craftingStationComponent;
    private Interactable? _interactable;
    private Transform? _usePoint;
    private Transform? _approachPoint;
    private Vector3 _approachPositionSnapshot;
    private Vector3 _usePositionSnapshot;
    private Vector3 _useForwardSnapshot;

    public int Id { get; }
    public int BuildingId { get; }
    public int ZoneId { get; }
    public string DisplayName { get; }
    public string PersistentFurnitureId { get; }
    public GameObject? FurnitureRoot => _furnitureRoot;
    public CraftingStation? CraftingStationComponent => _craftingStationComponent;
    public Interactable? Interactable => _interactable;
    public bool HasRuntimeBinding => _furnitureRoot != null && _craftingStationComponent != null && _interactable != null;
    public int? AssignedRegisteredNpcId { get; private set; }

    public Vector3 UsePosition
    {
        get
        {
            if (_usePoint != null)
            {
                _usePositionSnapshot = _usePoint.position;
            }

            return _usePositionSnapshot;
        }
    }

    public Vector3 UseForward => _useForwardSnapshot.sqrMagnitude > 0.0001f ? _useForwardSnapshot : Vector3.forward;

    public Vector3 ApproachPosition
    {
        get
        {
            if (_approachPoint != null)
            {
                _approachPositionSnapshot = _approachPoint.position;
            }

            return _approachPositionSnapshot;
        }
    }

    public RegisteredCraftStationData(
        int id,
        int buildingId,
        int zoneId,
        string displayName,
        string persistentFurnitureId,
        GameObject furnitureRoot,
        CraftingStation craftingStationComponent,
        Vector3 approachPosition,
        Vector3 useForward)
    {
        Id = id;
        BuildingId = buildingId;
        ZoneId = zoneId;
        DisplayName = displayName;
        PersistentFurnitureId = persistentFurnitureId;

        approachPosition.y = 0f;
        _approachPositionSnapshot = approachPosition;
        useForward.y = 0f;
        _useForwardSnapshot = useForward.sqrMagnitude > 0.0001f ? useForward.normalized : Vector3.zero;

        UpdateRuntimeBinding(furnitureRoot, craftingStationComponent);
    }

    public RegisteredCraftStationData(
        int id,
        int buildingId,
        int zoneId,
        string displayName,
        string persistentFurnitureId,
        Vector3 approachPosition,
        Vector3 usePosition,
        Vector3 useForward)
    {
        Id = id;
        BuildingId = buildingId;
        ZoneId = zoneId;
        DisplayName = displayName;
        PersistentFurnitureId = persistentFurnitureId;
        _approachPositionSnapshot = approachPosition;
        _usePositionSnapshot = usePosition;
        _useForwardSnapshot = useForward.sqrMagnitude > 0.0001f ? useForward.normalized : Vector3.forward;
    }

    public void UpdateRuntimeBinding(GameObject furnitureRoot, CraftingStation craftingStationComponent)
    {
        _furnitureRoot = furnitureRoot;
        _craftingStationComponent = craftingStationComponent;
        _interactable = craftingStationComponent as Interactable;
        _usePoint = EnsureUsePoint(furnitureRoot, _useForwardSnapshot);
        _usePositionSnapshot = _usePoint.position;

        if (_useForwardSnapshot.sqrMagnitude <= 0.0001f)
        {
            _useForwardSnapshot = ComputeUseForward(furnitureRoot, _usePositionSnapshot);
            _usePoint.rotation = Quaternion.LookRotation(_useForwardSnapshot, Vector3.up);
        }

        _approachPoint = EnsureApproachPoint(furnitureRoot, _usePositionSnapshot, _useForwardSnapshot, _approachPositionSnapshot);
        _approachPositionSnapshot = _approachPoint.position;
    }

    public void SetManualAnchor(Vector3 approachPosition, Vector3 usePosition, Vector3 useForward)
    {
        useForward.y = 0f;
        if (useForward.sqrMagnitude <= 0.0001f)
        {
            useForward = Vector3.forward;
        }

        _approachPositionSnapshot = approachPosition;
        _usePositionSnapshot = usePosition;
        _useForwardSnapshot = useForward.normalized;

        if (_furnitureRoot == null)
        {
            return;
        }

        _usePoint = EnsureManualPoint(_furnitureRoot, UsePointName, _usePositionSnapshot, _useForwardSnapshot);
        _approachPoint = EnsureManualPoint(_furnitureRoot, ApproachPointName, _approachPositionSnapshot, _useForwardSnapshot);
    }

    public void ClearRuntimeBinding()
    {
        _furnitureRoot = null;
        _craftingStationComponent = null;
        _interactable = null;
        _usePoint = null;
        _approachPoint = null;
    }

    public void AssignRegisteredNpc(int registeredNpcId)
    {
        AssignedRegisteredNpcId = registeredNpcId;
    }

    public void ClearAssignedRegisteredNpc()
    {
        AssignedRegisteredNpcId = null;
    }

    private static Transform EnsureUsePoint(GameObject furnitureRoot, Vector3 useForward)
    {
        var existing = furnitureRoot.transform.Find(UsePointName);
        if (existing == null)
        {
            var child = new GameObject(UsePointName);
            existing = child.transform;
            existing.SetParent(furnitureRoot.transform, false);
        }

        var usePosition = ComputeUsePosition(furnitureRoot);
        if (useForward.sqrMagnitude <= 0.0001f)
        {
            useForward = ComputeUseForward(furnitureRoot, usePosition);
        }

        existing.position = usePosition;
        existing.rotation = Quaternion.LookRotation(useForward.normalized, Vector3.up);
        return existing;
    }

    private static Transform EnsureApproachPoint(GameObject furnitureRoot, Vector3 usePosition, Vector3 useForward, Vector3 persistedApproachPosition)
    {
        var existing = furnitureRoot.transform.Find(ApproachPointName);
        if (existing == null)
        {
            var child = new GameObject(ApproachPointName);
            existing = child.transform;
            existing.SetParent(furnitureRoot.transform, false);
        }

        var approachPosition = persistedApproachPosition;
        if (approachPosition.sqrMagnitude <= 0.0001f)
        {
            approachPosition = ComputeApproachPosition(usePosition, useForward);
        }

        approachPosition = SnapToLocalFloor(approachPosition, usePosition.y);
        existing.position = approachPosition;
        existing.rotation = Quaternion.LookRotation(useForward.normalized, Vector3.up);
        return existing;
    }

    private static Transform EnsureManualPoint(GameObject furnitureRoot, string pointName, Vector3 position, Vector3 forward)
    {
        var existing = furnitureRoot.transform.Find(pointName);
        if (existing == null)
        {
            var child = new GameObject(pointName);
            existing = child.transform;
            existing.SetParent(furnitureRoot.transform, false);
        }

        existing.position = position;
        existing.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        return existing;
    }

    private static Vector3 ComputeUsePosition(GameObject furnitureRoot)
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
            return SnapToLocalFloor(furnitureRoot.transform.position, furnitureRoot.transform.position.y);
        }

        var forward = furnitureRoot.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = Vector3.forward;
        }

        forward.Normalize();
        var horizontalExtent = Mathf.Max(combinedBounds.extents.x, combinedBounds.extents.z);
        var standOff = Mathf.Max(0.75f, horizontalExtent + 0.35f);
        var candidate = combinedBounds.center + forward * standOff;
        return SnapToLocalFloor(candidate, combinedBounds.min.y);
    }

    private static Vector3 ComputeUseForward(GameObject furnitureRoot, Vector3 usePosition)
    {
        var target = furnitureRoot.transform.position - usePosition;
        target.y = 0f;
        if (target.sqrMagnitude > 0.0001f)
        {
            return target.normalized;
        }

        var fallback = furnitureRoot.transform.forward;
        fallback.y = 0f;
        return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.forward;
    }

    private static Vector3 ComputeApproachPosition(Vector3 usePosition, Vector3 useForward)
    {
        return usePosition - (useForward * ApproachDepth);
    }

    private static Vector3 SnapToLocalFloor(Vector3 candidate, float referenceY)
    {
        var rayOrigin = new Vector3(candidate.x, referenceY + 1.25f, candidate.z);
        const float rayDistance = 2.5f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out var hitInfo, rayDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            return hitInfo.point + Vector3.up * 0.05f;
        }

        candidate.y = referenceY + 0.05f;
        return candidate;
    }
}
