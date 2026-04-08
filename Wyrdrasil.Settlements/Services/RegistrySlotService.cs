using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using UnityEngine;
using Wyrdrasil.Registry.Tool;
using Wyrdrasil.Core.Services;
using Wyrdrasil.Settlements.Tool;
using Wyrdrasil.Settlements.Components;

namespace Wyrdrasil.Settlements.Services;


public sealed class RegistrySlotService
{
    private readonly ManualLogSource _log;
    private readonly RegistryZoneService _zoneService;
    private readonly RegistryAnchorPolicyService _anchorPolicyService;
    private readonly List<ZoneSlotData> _slots = new();
    private readonly Dictionary<int, WyrdrasilZoneSlotMarker> _markers = new();
    private readonly Dictionary<int, GameObject> _slotFollowTargets = new();
    private readonly Dictionary<int, GameObject> _slotRoots = new();

    private int _nextSlotId = 1;
    private bool _visualsVisible;
    private int? _pendingForceAssignTargetSlotId;

    public IReadOnlyList<ZoneSlotData> Slots => _slots;
    public int NextSlotId => _nextSlotId;

    public RegistrySlotService(ManualLogSource log, RegistryModeService modeService, RegistryZoneService zoneService, RegistryAnchorPolicyService anchorPolicyService)
    {
        _log = log;
        _zoneService = zoneService;
        _anchorPolicyService = anchorPolicyService;
        _visualsVisible = modeService.IsRegistryModeEnabled;
        modeService.RegistryModeChanged += OnRegistryModeChanged;
    }

    public void LoadSlots(IEnumerable<ZoneSlotData> slots, int nextSlotId)
    {
        foreach (var root in _slotRoots.Values)
        {
            if (root != null) Object.Destroy(root);
        }

        _slots.Clear();
        _markers.Clear();
        _slotRoots.Clear();
        _slotFollowTargets.Clear();

        foreach (var slot in slots)
        {
            _slots.Add(slot);
            CreateSlotWorldObject(slot);
        }

        _nextSlotId = nextSlotId;
    }

    public void ClearAllSlots()
    {
        foreach (var root in _slotRoots.Values)
        {
            if (root != null) Object.Destroy(root);
        }

        _slots.Clear();
        _markers.Clear();
        _slotRoots.Clear();
        _slotFollowTargets.Clear();
        _pendingForceAssignTargetSlotId = null;
        _nextSlotId = 1;
    }

    public bool TryRestoreAssignment(int slotId, int residentId)
    {
        var slot = _slots.FirstOrDefault(candidate => candidate.Id == slotId);
        if (slot == null) return false;
        slot.AssignRegisteredNpc(residentId);
        UpdateSlotVisual(slot);
        return true;
    }

    public bool TryGetSlotById(int slotId, out ZoneSlotData slotData)
    {
        var slot = _slots.FirstOrDefault(candidate => candidate.Id == slotId);
        if (slot == null)
        {
            slotData = null!;
            return false;
        }

        slotData = slot;
        return true;
    }

    public void CreateInnkeeperSlot() => CreateSlot(ZoneSlotType.Innkeeper);
    public bool TryGetPlacementPoint(out Vector3 placementPoint) => _zoneService.TryGetPlacementPoint(out placementPoint);

    public bool TryAssignInnkeeperSlot(int registeredNpcId, out ZoneSlotData? slotData)
    {
        foreach (var slot in _slots)
        {
            if (slot.SlotType != ZoneSlotType.Innkeeper || slot.AssignedRegisteredNpcId.HasValue) continue;
            slot.AssignRegisteredNpc(registeredNpcId);
            UpdateSlotVisual(slot);
            slotData = slot;
            return true;
        }

        slotData = null;
        return false;
    }

    public bool TryGetSlotAtCrosshair(out ZoneSlotData slotData)
    {
        var activeCamera = Camera.main;
        if (activeCamera != null)
        {
            var ray = new Ray(activeCamera.transform.position, activeCamera.transform.forward);
            if (Physics.Raycast(ray, out var hitInfo, 100f, ~0, QueryTriggerInteraction.Ignore))
            {
                return TryFindSlotAtPoint(hitInfo.point, out slotData);
            }
        }

        slotData = null!;
        return false;
    }

    public void SetPendingForceAssignTarget(int? slotId)
    {
        if (_pendingForceAssignTargetSlotId == slotId) return;
        _pendingForceAssignTargetSlotId = slotId;
        foreach (var slot in _slots) UpdateSlotVisual(slot);
    }

    public bool ClearSlotAssignment(int slotId, out int? previousResidentId)
    {
        foreach (var slot in _slots)
        {
            if (slot.Id != slotId) continue;
            previousResidentId = slot.AssignedRegisteredNpcId;
            if (!previousResidentId.HasValue) return false;
            slot.ClearAssignedRegisteredNpc();
            UpdateSlotVisual(slot);
            return true;
        }

        previousResidentId = null;
        return false;
    }

    public bool ForceAssignInnkeeperSlot(int slotId, int registeredNpcId, out int? previousResidentId, out ZoneSlotData? slotData)
    {
        foreach (var slot in _slots)
        {
            if (slot.Id != slotId) continue;
            previousResidentId = slot.AssignedRegisteredNpcId;
            slot.AssignRegisteredNpc(registeredNpcId);
            UpdateSlotVisual(slot);
            slotData = slot;
            return true;
        }

        previousResidentId = null;
        slotData = null;
        return false;
    }

    public void ClearAssignmentForResident(int registeredNpcId)
    {
        foreach (var slot in _slots.Where(slot => slot.AssignedRegisteredNpcId == registeredNpcId))
        {
            slot.ClearAssignedRegisteredNpc();
            UpdateSlotVisual(slot);
        }
    }

    public bool TryGetSlotFollowTarget(int slotId, out GameObject followTarget) => _slotFollowTargets.TryGetValue(slotId, out followTarget!);

    public bool TryFindSlotAtPoint(Vector3 point, out ZoneSlotData slotData)
    {
        var bestMatch = _slots.Where(slot => slot.SlotType == ZoneSlotType.Innkeeper)
            .OrderBy(slot => Vector3.Distance(point, slot.Position))
            .FirstOrDefault(slot => Vector3.Distance(point, slot.Position) < 1.2f);

        if (bestMatch == null)
        {
            slotData = null!;
            return false;
        }

        slotData = bestMatch;
        return true;
    }

    public bool DeleteSlot(int slotId)
    {
        var removed = _slots.RemoveAll(slot => slot.Id == slotId) > 0;
        if (!removed) return false;
        if (_slotRoots.TryGetValue(slotId, out var root) && root != null) Object.Destroy(root);
        _slotRoots.Remove(slotId);
        _slotFollowTargets.Remove(slotId);
        _markers.Remove(slotId);
        return true;
    }

    public IReadOnlyList<int> DeleteSlotsInZone(int zoneId)
    {
        var slotIds = _slots.Where(slot => slot.ZoneId == zoneId).Select(slot => slot.Id).ToArray();
        foreach (var slotId in slotIds) DeleteSlot(slotId);
        return slotIds;
    }

    private void CreateSlot(ZoneSlotType slotType)
    {
        if (!TryGetPlacementPoint(out var placementPoint))
        {
            _log.LogWarning($"Cannot create slot '{slotType}': no valid support surface was found under the crosshair.");
            return;
        }

        var zone = _zoneService.FindZoneContainingPointHorizontally(placementPoint);
        if (zone == null)
        {
            _log.LogWarning($"Cannot create slot '{slotType}': the placement point is not inside any functional zone footprint.");
            return;
        }

        if (_anchorPolicyService.RequiresZone(slotType) && !_anchorPolicyService.IsZoneTypeAllowed(slotType, zone.ZoneType))
        {
            _log.LogWarning($"Cannot create slot '{slotType}': zone type '{zone.ZoneType}' is not compatible.");
            return;
        }

        var slotData = new ZoneSlotData(_nextSlotId++, zone.BuildingId, zone.Id, slotType, placementPoint);
        _slots.Add(slotData);
        CreateSlotWorldObject(slotData);
        _log.LogInfo($"Created {slotType} slot #{slotData.Id} in zone #{zone.Id} (building #{zone.BuildingId}) at {slotData.Position}.");
    }

    private void OnRegistryModeChanged(bool isEnabled)
    {
        _visualsVisible = isEnabled;
        foreach (var marker in _markers.Values) marker.SetVisualizationVisible(isEnabled);
    }

    private void CreateSlotWorldObject(ZoneSlotData slotData)
    {
        var root = new GameObject($"Wyrdrasil_ZoneSlot_{slotData.SlotType}_{slotData.Id}");
        root.transform.position = slotData.Position;
        _slotRoots[slotData.Id] = root;
        _slotFollowTargets[slotData.Id] = root;

        var marker = root.AddComponent<WyrdrasilZoneSlotMarker>();
        marker.Initialize(slotData.Id, slotData.ZoneId, slotData.SlotType);

        var visual = GameObject.CreatePrimitive(slotData.SlotType == ZoneSlotType.Innkeeper ? PrimitiveType.Cube : PrimitiveType.Sphere);
        visual.transform.SetParent(root.transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localScale = slotData.SlotType == ZoneSlotType.Innkeeper ? new Vector3(0.45f, 0.9f, 0.45f) : new Vector3(0.35f, 0.35f, 0.35f);
        var collider = visual.GetComponent<Collider>();
        if (collider != null) Object.Destroy(collider);
        var renderer = visual.GetComponent<Renderer>();
        if (renderer != null)
        {
            var material = CreateVisualMaterial(slotData.SlotType, slotData.AssignedRegisteredNpcId.HasValue);
            if (material != null) renderer.material = material;
        }

        marker.RegisterRenderer(renderer);
        marker.SetVisualizationVisible(_visualsVisible);
        _markers[slotData.Id] = marker;
        UpdateSlotVisual(slotData);
    }

    private void UpdateSlotVisual(ZoneSlotData slotData)
    {
        if (_markers.TryGetValue(slotData.Id, out var marker))
        {
            marker.SetOccupied(slotData.AssignedRegisteredNpcId.HasValue);
            marker.SetPendingForceAssignTarget(_pendingForceAssignTargetSlotId == slotData.Id);
        }
    }

    private static Material? CreateVisualMaterial(ZoneSlotType slotType, bool isOccupied)
    {
        var shader = Shader.Find("Sprites/Default");
        if (!shader)
        {
            shader = Shader.Find("Unlit/Color");
        }

        if (!shader)
        {
            return null;
        }
        var material = new Material(shader);
        material.color = isOccupied ? new Color(0.35f, 1f, 0.35f, 1f) : slotType == ZoneSlotType.Innkeeper ? new Color(0.2f, 0.8f, 0.95f, 1f) : new Color(0.95f, 0.95f, 0.35f, 1f);
        return material;
    }
}
