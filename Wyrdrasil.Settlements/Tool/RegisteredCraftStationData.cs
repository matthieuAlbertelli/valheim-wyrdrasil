using UnityEngine;

namespace Wyrdrasil.Settlements.Tool;

public sealed class RegisteredCraftStationData
{
    private GameObject? _furnitureRoot;
    private CraftingStation? _craftingStationComponent;
    private Interactable? _interactable;
    private Vector3 _anchorLocalPosition;
    private Vector3 _anchorLocalForward;
    private Vector3 _referenceWorldPositionSnapshot;

    public int Id { get; }
    public int BuildingId { get; }
    public int ZoneId { get; }
    public string DisplayName { get; }
    public string PersistentFurnitureId { get; }
    public string InteractionProfileId { get; private set; }

    public GameObject? FurnitureRoot => _furnitureRoot;
    public CraftingStation? CraftingStationComponent => _craftingStationComponent;
    public Interactable? Interactable => _interactable;
    public bool HasRuntimeBinding => _furnitureRoot != null && _craftingStationComponent != null && _interactable != null;
    public int? AssignedRegisteredNpcId { get; private set; }
    public InteractionAnchorPose AnchorPoseLocal => new(_anchorLocalPosition, _anchorLocalForward);
    public Vector3 ReferenceWorldPosition => _furnitureRoot != null ? _furnitureRoot.transform.position : _referenceWorldPositionSnapshot;

    public RegisteredCraftStationData(
        int id,
        int buildingId,
        int zoneId,
        string displayName,
        string persistentFurnitureId,
        Vector3 referenceWorldPosition,
        Vector3 anchorLocalPosition,
        Vector3 anchorLocalForward,
        string interactionProfileId)
    {
        Id = id;
        BuildingId = buildingId;
        ZoneId = zoneId;
        DisplayName = displayName;
        PersistentFurnitureId = persistentFurnitureId;
        _referenceWorldPositionSnapshot = referenceWorldPosition;
        InteractionProfileId = interactionProfileId;
        SetManualAnchorLocal(anchorLocalPosition, anchorLocalForward);
    }

    public void UpdateRuntimeBinding(GameObject furnitureRoot, CraftingStation craftingStationComponent)
    {
        _furnitureRoot = furnitureRoot;
        _craftingStationComponent = craftingStationComponent;
        _interactable = craftingStationComponent as Interactable;
        _referenceWorldPositionSnapshot = furnitureRoot.transform.position;
    }

    public void ClearRuntimeBinding()
    {
        _furnitureRoot = null;
        _craftingStationComponent = null;
        _interactable = null;
    }

    public bool TryResolveWorldAnchor(out Vector3 anchorWorldPosition, out Vector3 anchorWorldForward)
    {
        if (_furnitureRoot == null)
        {
            anchorWorldPosition = Vector3.zero;
            anchorWorldForward = Vector3.forward;
            return false;
        }

        var root = _furnitureRoot.transform;
        anchorWorldPosition = root.TransformPoint(_anchorLocalPosition);
        anchorWorldForward = root.TransformDirection(_anchorLocalForward);
        anchorWorldForward.y = 0f;
        if (anchorWorldForward.sqrMagnitude <= 0.0001f)
        {
            anchorWorldForward = root.forward;
            anchorWorldForward.y = 0f;
        }

        if (anchorWorldForward.sqrMagnitude <= 0.0001f)
        {
            anchorWorldForward = Vector3.forward;
        }

        anchorWorldForward.Normalize();
        return true;
    }

    public void SetManualAnchorLocal(Vector3 localPosition, Vector3 localForward)
    {
        _anchorLocalPosition = localPosition;
        localForward.y = 0f;
        _anchorLocalForward = localForward.sqrMagnitude > 0.0001f
            ? localForward.normalized
            : Vector3.forward;
    }

    public bool SetManualAnchorWorld(Vector3 worldPosition, Vector3 worldForward)
    {
        if (_furnitureRoot == null)
        {
            return false;
        }

        var root = _furnitureRoot.transform;
        var localPosition = root.InverseTransformPoint(worldPosition);
        var localForward = root.InverseTransformDirection(worldForward);
        localForward.y = 0f;
        SetManualAnchorLocal(localPosition, localForward);
        return true;
    }

    public void ApplyProfile(CraftStationInteractionProfile profile)
    {
        InteractionProfileId = profile.ProfileId;
        SetManualAnchorLocal(profile.DefaultLocalAnchorPosition, profile.DefaultLocalAnchorForward);
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
