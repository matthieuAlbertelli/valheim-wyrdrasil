using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using UnityEngine;
using Wyrdrasil.Registry.Components;
using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Services;

public sealed class RegistrySlotService
{
    private readonly ManualLogSource _log;
    private readonly RegistryZoneService _zoneService;
    private readonly List<ZoneSlotData> _slots = new();
    private readonly Dictionary<int, WyrdrasilZoneSlotMarker> _markers = new();
    private readonly Dictionary<int, GameObject> _slotFollowTargets = new();
    private readonly Dictionary<int, GameObject> _slotRoots = new();

    private int _nextSlotId = 1;
    private bool _visualsVisible;

    public IReadOnlyList<ZoneSlotData> Slots => _slots;

    public RegistrySlotService(ManualLogSource log, RegistryModeService modeService, RegistryZoneService zoneService)
    {
        _log = log;
        _zoneService = zoneService;
        _visualsVisible = modeService.IsRegistryModeEnabled;
        modeService.RegistryModeChanged += OnRegistryModeChanged;
    }

    public void CreateInnkeeperSlot() => CreateSlot(ZoneSlotType.Innkeeper);

    public bool TryGetPlacementPoint(out Vector3 placementPoint)
    {
        var localPlayer = Player.m_localPlayer;
        if (!localPlayer)
        {
            placementPoint = Vector3.zero;
            return false;
        }

        var activeCamera = Camera.main;
        if (activeCamera != null)
        {
            var ray = new Ray(activeCamera.transform.position, activeCamera.transform.forward);
            if (Physics.Raycast(ray, out var hitInfo, 100f, ~0, QueryTriggerInteraction.Ignore))
            {
                placementPoint = hitInfo.point;
                placementPoint.y += 0.05f;
                return true;
            }
        }

        placementPoint = localPlayer.transform.position + localPlayer.transform.forward * 2f;
        return true;
    }

    public bool TryAssignInnkeeperSlot(int registeredNpcId, out ZoneSlotData? slotData)
    {
        foreach (var slot in _slots)
        {
            if (slot.SlotType != ZoneSlotType.Innkeeper || slot.AssignedRegisteredNpcId.HasValue)
            {
                continue;
            }

            slot.AssignRegisteredNpc(registeredNpcId);
            UpdateSlotVisual(slot);
            slotData = slot;
            return true;
        }

        slotData = null;
        return false;
    }


    public void ClearAssignmentForResident(int registeredNpcId)
    {
        foreach (var slot in _slots)
        {
            if (slot.AssignedRegisteredNpcId != registeredNpcId)
            {
                continue;
            }

            slot.ClearAssignedRegisteredNpc();
            UpdateSlotVisual(slot);
        }
    }

    public bool TryGetSlotFollowTarget(int slotId, out GameObject followTarget) => _slotFollowTargets.TryGetValue(slotId, out followTarget!);

    public bool TryFindSlotAtPoint(Vector3 point, out ZoneSlotData slotData)
    {
        ZoneSlotData? bestMatch = null;
        var bestDistance = float.MaxValue;
        foreach (var slot in _slots)
        {
            if (slot.SlotType != ZoneSlotType.Innkeeper)
            {
                continue;
            }

            var distance = Vector3.Distance(point, slot.Position);
            if (distance >= 1.2f || distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestMatch = slot;
        }

        if (bestMatch != null)
        {
            slotData = bestMatch;
            return true;
        }

        slotData = null!;
        return false;
    }

    public bool DeleteSlot(int slotId)
    {
        var removed = _slots.RemoveAll(slot => slot.Id == slotId) > 0;
        if (!removed)
        {
            return false;
        }

        if (_slotRoots.TryGetValue(slotId, out var root) && root != null)
        {
            Object.Destroy(root);
        }

        _slotRoots.Remove(slotId);
        _slotFollowTargets.Remove(slotId);
        _markers.Remove(slotId);
        return true;
    }

    public IReadOnlyList<int> DeleteSlotsInZone(int zoneId)
    {
        var slotIds = _slots.Where(slot => slot.ZoneId == zoneId).Select(slot => slot.Id).ToArray();
        foreach (var slotId in slotIds)
        {
            DeleteSlot(slotId);
        }
        return slotIds;
    }

    private void CreateSlot(ZoneSlotType slotType)
    {
        if (!TryGetPlacementPoint(out var placementPoint))
        {
            _log.LogWarning($"Cannot create slot '{slotType}': no valid placement point was found.");
            return;
        }

        var tavernZone = _zoneService.FindZoneContainingPoint(placementPoint, ZoneType.Tavern);
        if (tavernZone == null)
        {
            _log.LogWarning($"Cannot create slot '{slotType}': the placement point is not inside a Tavern zone.");
            return;
        }

        var slotId = _nextSlotId++;
        var slotData = new ZoneSlotData(slotId, tavernZone.Id, slotType, placementPoint);
        _slots.Add(slotData);
        CreateSlotWorldObject(slotData);
        _log.LogInfo($"Created {slotType} slot #{slotData.Id} in Tavern zone #{tavernZone.Id} at {slotData.Position}.");
    }

    private void OnRegistryModeChanged(bool isEnabled)
    {
        _visualsVisible = isEnabled;
        foreach (var marker in _markers.Values)
        {
            marker.SetVisualizationVisible(isEnabled);
        }
    }

    private void CreateSlotWorldObject(ZoneSlotData slotData)
    {
        var root = new GameObject($"Wyrdrasil_ZoneSlot_{slotData.SlotType}_{slotData.Id}");
        root.transform.position = slotData.Position;
        _slotRoots[slotData.Id] = root;
        _slotFollowTargets[slotData.Id] = root;

        var marker = root.AddComponent<WyrdrasilZoneSlotMarker>();
        marker.Initialize(slotData.Id, slotData.ZoneId, slotData.SlotType);

        var visualType = slotData.SlotType == ZoneSlotType.Innkeeper ? PrimitiveType.Cube : PrimitiveType.Sphere;
        var visual = GameObject.CreatePrimitive(visualType);
        visual.name = "SlotVisual";
        visual.transform.SetParent(root.transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localScale = slotData.SlotType == ZoneSlotType.Innkeeper ? new Vector3(0.45f, 0.9f, 0.45f) : new Vector3(0.35f, 0.35f, 0.35f);

        var collider = visual.GetComponent<Collider>();
        if (collider != null)
        {
            Object.Destroy(collider);
        }

        var renderer = visual.GetComponent<Renderer>();
        if (renderer != null)
        {
            var material = CreateVisualMaterial(slotData.SlotType, slotData.AssignedRegisteredNpcId.HasValue);
            if (material != null)
            {
                renderer.material = material;
            }
        }

        marker.RegisterRenderer(renderer);
        marker.SetVisualizationVisible(_visualsVisible);
        _markers[slotData.Id] = marker;
    }

    private void UpdateSlotVisual(ZoneSlotData slotData)
    {
        if (_markers.TryGetValue(slotData.Id, out var marker))
        {
            marker.SetOccupied(slotData.AssignedRegisteredNpcId.HasValue);
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
