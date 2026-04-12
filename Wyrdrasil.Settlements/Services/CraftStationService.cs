using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using UnityEngine;
using Wyrdrasil.Core.Services;
using Wyrdrasil.Settlements.Components;
using Wyrdrasil.Settlements.Tool;

namespace Wyrdrasil.Settlements.Services;

public sealed class CraftStationService
{
    private const float ResolveStationDistance = 2.5f;

    private readonly ManualLogSource _log;
    private readonly FunctionalZoneService _zoneService;
    private readonly List<RegisteredCraftStationData> _craftStations = new();
    private readonly Dictionary<int, WyrdrasilRegisteredCraftStationMarker> _markers = new();
    private readonly Dictionary<int, GameObject> _anchorRoots = new();
    private int _nextCraftStationId = 1;
    private bool _visualsVisible;

    public IReadOnlyList<RegisteredCraftStationData> CraftStations => _craftStations;
    public int NextCraftStationId => _nextCraftStationId;

    public CraftStationService(ManualLogSource log, RegistryModeService modeService, FunctionalZoneService zoneService)
    {
        _log = log;
        _zoneService = zoneService;
        _visualsVisible = modeService.IsRegistryModeEnabled;
        modeService.RegistryModeChanged += OnRegistryModeChanged;
    }

    public void LoadCraftStations(IEnumerable<RegisteredCraftStationData> craftStations, int nextCraftStationId)
    {
        foreach (var station in _craftStations)
        {
            if (_markers.TryGetValue(station.Id, out var marker) && marker != null)
            {
                marker.SetVisualizationVisible(false, false);
            }
        }

        foreach (var root in _anchorRoots.Values)
        {
            if (root != null)
            {
                Object.Destroy(root);
            }
        }

        _craftStations.Clear();
        _markers.Clear();
        _anchorRoots.Clear();

        foreach (var station in craftStations)
        {
            _craftStations.Add(station);
            EnsureMarker(station);
            EnsureAnchorIndicator(station);
            UpdateMarker(station);
            UpdateAnchorIndicator(station);
        }

        _nextCraftStationId = nextCraftStationId;
    }

    public void ClearAllCraftStations()
    {
        foreach (var station in _craftStations)
        {
            if (_markers.TryGetValue(station.Id, out var marker) && marker != null)
            {
                marker.SetVisualizationVisible(false, false);
            }
        }

        foreach (var root in _anchorRoots.Values)
        {
            if (root != null)
            {
                Object.Destroy(root);
            }
        }

        _craftStations.Clear();
        _markers.Clear();
        _anchorRoots.Clear();
        _nextCraftStationId = 1;
    }

    public bool TryRestoreAssignment(int craftStationId, int residentId)
    {
        var station = _craftStations.FirstOrDefault(candidate => candidate.Id == craftStationId);
        if (station == null)
        {
            return false;
        }

        station.AssignRegisteredNpc(residentId);
        UpdateMarker(station);
        return true;
    }

    public bool TryGetCraftStationById(int craftStationId, out RegisteredCraftStationData craftStationData)
    {
        var station = _craftStations.FirstOrDefault(candidate => candidate.Id == craftStationId);
        if (station == null)
        {
            craftStationData = null!;
            return false;
        }

        craftStationData = station;
        return true;
    }

    public bool TryResolveCraftStationFromSave(RegisteredCraftStationSaveData saveData, out RegisteredCraftStationData craftStationData)
    {
        var referencePosition = saveData.ReferenceWorldPosition.ToVector3();
        var allStations = Object.FindObjectsByType<CraftingStation>(FindObjectsSortMode.None);

        var exactCandidates = allStations
            .Select(station => new
            {
                Station = station,
                PersistentId = BuildPersistentFurnitureId(station),
                Distance = Vector3.Distance(GetReferencePosition(station), referencePosition)
            })
            .Where(candidate => candidate.PersistentId == saveData.PersistentFurnitureId)
            .OrderBy(candidate => candidate.Distance)
            .ToList();

        if (exactCandidates.Count > 0)
        {
            var exactMatch = exactCandidates[0];
            if (exactMatch.Distance <= ResolveStationDistance)
            {
                craftStationData = BuildResolvedCraftStation(saveData, exactMatch.Station, exactMatch.PersistentId);
                _log.LogInfo($"[CraftStation][Resolve] Restored station #{craftStationData.Id} with exact persistent match '{exactMatch.PersistentId}'.");
                return true;
            }
        }

        var fallbackStation = allStations
            .OrderBy(station => Vector3.Distance(GetReferencePosition(station), referencePosition))
            .FirstOrDefault();

        if (fallbackStation != null)
        {
            var fallbackDistance = Vector3.Distance(GetReferencePosition(fallbackStation), referencePosition);
            if (fallbackDistance <= ResolveStationDistance)
            {
                craftStationData = BuildResolvedCraftStation(saveData, fallbackStation, BuildPersistentFurnitureId(fallbackStation));
                _log.LogWarning($"[CraftStation][Resolve] Restored station #{craftStationData.Id} with fallback nearest-station resolution. distance={fallbackDistance:0.00}");
                return true;
            }
        }

        craftStationData = null!;
        _log.LogWarning($"[CraftStation][Resolve] Unable to resolve craft station #{saveData.Id} ('{saveData.DisplayName}') near {referencePosition}.");
        return false;
    }

    public void DesignateCraftStationAtCrosshair()
    {
        if (!TryGetCraftStationAtCrosshair(out var furnitureRoot, out var craftingStation))
        {
            _log.LogWarning("[CraftStation][Authoring] Cannot designate craft station: targeted object is not a valid Valheim crafting station.");
            return;
        }

        if (FindCraftStationByFurniture(furnitureRoot) != null)
        {
            _log.LogWarning("[CraftStation][Authoring] Cannot designate craft station: this furniture is already registered.");
            return;
        }

        var zone = _zoneService.FindZoneContainingPointHorizontally(GetReferencePosition(craftingStation));
        if (zone == null)
        {
            _log.LogWarning("[CraftStation][Authoring] Cannot designate craft station yet: no functional zone footprint found at the targeted table.");
            return;
        }

        var persistentFurnitureId = BuildPersistentFurnitureId(craftingStation);
        var referenceWorldPosition = GetReferencePosition(craftingStation);
        var profile = ResolveProfileForFurniture(furnitureRoot.name);

        var data = new RegisteredCraftStationData(
            _nextCraftStationId++,
            zone.BuildingId,
            zone.Id,
            furnitureRoot.name,
            persistentFurnitureId,
            referenceWorldPosition,
            profile.DefaultLocalAnchorPosition,
            profile.DefaultLocalAnchorForward,
            profile.ProfileId);

        data.UpdateRuntimeBinding(furnitureRoot, craftingStation);
        _craftStations.Add(data);
        EnsureMarker(data);
        EnsureAnchorIndicator(data);
        UpdateMarker(data);
        UpdateAnchorIndicator(data);

        _log.LogInfo($"[CraftStation][Authoring] Designated station #{data.Id} on '{data.DisplayName}' in zone #{zone.Id} (building #{zone.BuildingId}) with profile='{profile.ProfileId}' and persistentId='{persistentFurnitureId}'.");
    }

    public bool TryGetCraftStationAtCrosshair(out RegisteredCraftStationData craftStationData)
    {
        if (!TryGetCraftStationAtCrosshair(out var furnitureRoot, out _))
        {
            craftStationData = null!;
            return false;
        }

        var existingStation = FindCraftStationByFurniture(furnitureRoot);
        if (existingStation == null)
        {
            craftStationData = null!;
            return false;
        }

        craftStationData = existingStation;
        return true;
    }

    public bool TryGetInteractionProfile(RegisteredCraftStationData station, out CraftStationInteractionProfile profile)
    {
        if (CraftStationInteractionProfileRegistry.TryGetProfileById(station.InteractionProfileId, out profile))
        {
            return true;
        }

        profile = CraftStationInteractionProfileRegistry.GetDefaultProfile();
        return true;
    }

    public bool ForceAssignCraftStation(int craftStationId, int registeredNpcId, out int? previousResidentId, out RegisteredCraftStationData? craftStationData)
    {
        foreach (var station in _craftStations)
        {
            if (station.Id != craftStationId)
            {
                continue;
            }

            previousResidentId = station.AssignedRegisteredNpcId;
            station.AssignRegisteredNpc(registeredNpcId);
            UpdateMarker(station);
            craftStationData = station;
            _log.LogInfo($"[CraftStation][Assignment] Assigned resident #{registeredNpcId} to station #{station.Id}. previousResident={previousResidentId?.ToString() ?? "none"}");
            return true;
        }

        previousResidentId = null;
        craftStationData = null;
        return false;
    }

    public bool ClearCraftStationAssignment(int craftStationId, out int? previousResidentId)
    {
        foreach (var station in _craftStations)
        {
            if (station.Id != craftStationId)
            {
                continue;
            }

            previousResidentId = station.AssignedRegisteredNpcId;
            if (!previousResidentId.HasValue)
            {
                return false;
            }

            station.ClearAssignedRegisteredNpc();
            UpdateMarker(station);
            _log.LogInfo($"[CraftStation][Assignment] Cleared assignment on station #{station.Id}. previousResident={previousResidentId.Value}");
            return true;
        }

        previousResidentId = null;
        return false;
    }

    public void ClearAssignmentForResident(int registeredNpcId)
    {
        foreach (var station in _craftStations.Where(candidate => candidate.AssignedRegisteredNpcId == registeredNpcId))
        {
            station.ClearAssignedRegisteredNpc();
            UpdateMarker(station);
            _log.LogInfo($"[CraftStation][Assignment] Cleared resident #{registeredNpcId} from station #{station.Id}.");
        }
    }

    public bool DeleteCraftStationAtCrosshair(out int craftStationId)
    {
        craftStationId = 0;
        if (!TryGetCraftStationAtCrosshair(out var furnitureRoot, out _))
        {
            return false;
        }

        var existingStation = FindCraftStationByFurniture(furnitureRoot);
        if (existingStation == null)
        {
            return false;
        }

        craftStationId = existingStation.Id;
        return DeleteCraftStation(existingStation.Id);
    }

    public IReadOnlyList<int> DeleteCraftStationsInZone(int zoneId)
    {
        var stationIds = new List<int>();
        foreach (var station in new List<RegisteredCraftStationData>(_craftStations))
        {
            if (station.ZoneId != zoneId)
            {
                continue;
            }

            stationIds.Add(station.Id);
            DeleteCraftStation(station.Id);
        }

        return stationIds;
    }

    public bool DeleteCraftStation(int craftStationId)
    {
        var index = _craftStations.FindIndex(candidate => candidate.Id == craftStationId);
        if (index < 0)
        {
            return false;
        }

        var station = _craftStations[index];
        if (_markers.TryGetValue(station.Id, out var marker) && marker != null)
        {
            marker.SetVisualizationVisible(false, false);
        }

        if (_anchorRoots.TryGetValue(station.Id, out var anchorRoot) && anchorRoot != null)
        {
            Object.Destroy(anchorRoot);
        }

        _markers.Remove(station.Id);
        _anchorRoots.Remove(station.Id);
        _craftStations.RemoveAt(index);
        _log.LogInfo($"[CraftStation][Authoring] Deleted station #{station.Id} ('{station.DisplayName}').");
        return true;
    }

    private void OnRegistryModeChanged(bool isEnabled)
    {
        _visualsVisible = isEnabled;
        foreach (var station in _craftStations)
        {
            UpdateMarker(station);
            UpdateAnchorIndicator(station);
        }
    }

    private void EnsureMarker(RegisteredCraftStationData station)
    {
        if (station.FurnitureRoot == null || _markers.ContainsKey(station.Id))
        {
            return;
        }

        var marker = station.FurnitureRoot.GetComponent<WyrdrasilRegisteredCraftStationMarker>();
        if (marker == null)
        {
            marker = station.FurnitureRoot.AddComponent<WyrdrasilRegisteredCraftStationMarker>();
        }

        marker.Initialize(station.Id);
        marker.RegisterRenderers(station.FurnitureRoot.GetComponentsInChildren<Renderer>(true));
        _markers[station.Id] = marker;
    }

    private void UpdateMarker(RegisteredCraftStationData station)
    {
        if (_markers.TryGetValue(station.Id, out var marker) && marker != null)
        {
            marker.SetVisualizationVisible(_visualsVisible, station.AssignedRegisteredNpcId.HasValue);
        }
    }

    private void EnsureAnchorIndicator(RegisteredCraftStationData station)
    {
        if (_anchorRoots.ContainsKey(station.Id))
        {
            return;
        }

        var root = new GameObject($"Wyrdrasil_CraftStationAnchor_{station.Id}");
        _anchorRoots[station.Id] = root;

        var anchor = CreateIndicatorPrimitive(root.transform, PrimitiveType.Sphere, "Anchor", new Vector3(0.18f, 0.18f, 0.18f), new Color(0.25f, 1f, 0.35f, 1f));
        var shaft = CreateIndicatorPrimitive(root.transform, PrimitiveType.Cube, "ArrowShaft", new Vector3(0.08f, 0.08f, 0.65f), new Color(1f, 0.25f, 0.25f, 1f));
        var head = CreateIndicatorPrimitive(root.transform, PrimitiveType.Cube, "ArrowHead", new Vector3(0.18f, 0.18f, 0.18f), new Color(1f, 0.25f, 0.25f, 1f));

        anchor.transform.localPosition = Vector3.zero;
        shaft.transform.localPosition = new Vector3(0f, 0f, 0.38f);
        head.transform.localPosition = new Vector3(0f, 0f, 0.72f);

        UpdateAnchorIndicator(station);
    }

    private void UpdateAnchorIndicator(RegisteredCraftStationData station)
    {
        if (!_anchorRoots.TryGetValue(station.Id, out var root) || root == null)
        {
            return;
        }

        root.SetActive(_visualsVisible);
        if (!_visualsVisible)
        {
            return;
        }

        if (!station.TryResolveWorldAnchor(out var anchorWorldPosition, out var anchorWorldForward))
        {
            root.SetActive(false);
            return;
        }

        root.transform.position = anchorWorldPosition + Vector3.up * 0.05f;
        root.transform.rotation = Quaternion.LookRotation(anchorWorldForward, Vector3.up);
    }

    private static GameObject CreateIndicatorPrimitive(Transform parent, PrimitiveType primitiveType, string name, Vector3 scale, Color color)
    {
        var gameObject = GameObject.CreatePrimitive(primitiveType);
        gameObject.name = name;
        gameObject.transform.SetParent(parent, false);
        gameObject.transform.localScale = scale;

        var collider = gameObject.GetComponent<Collider>();
        if (collider != null)
        {
            Object.Destroy(collider);
        }

        var renderer = gameObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            var shader = Shader.Find("Sprites/Default");
            if (!shader)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader)
            {
                var material = new Material(shader);
                material.color = color;
                renderer.material = material;
            }
        }

        return gameObject;
    }

    private RegisteredCraftStationData BuildResolvedCraftStation(RegisteredCraftStationSaveData saveData, CraftingStation runtimeStation, string persistentId)
    {
        var profile = ResolveProfileForRestoredStation(saveData, runtimeStation.gameObject.name);
        var craftStationData = new RegisteredCraftStationData(
            saveData.Id,
            saveData.BuildingId,
            saveData.ZoneId,
            saveData.DisplayName,
            persistentId,
            saveData.ReferenceWorldPosition.ToVector3(),
            saveData.AnchorLocalPosition.ToVector3(),
            saveData.AnchorLocalForward.ToVector3(),
            profile.ProfileId);

        craftStationData.UpdateRuntimeBinding(runtimeStation.gameObject, runtimeStation);
        if (saveData.AssignedRegisteredNpcId.HasValue)
        {
            craftStationData.AssignRegisteredNpc(saveData.AssignedRegisteredNpcId.Value);
        }

        EnsureMarker(craftStationData);
        EnsureAnchorIndicator(craftStationData);
        UpdateMarker(craftStationData);
        UpdateAnchorIndicator(craftStationData);
        return craftStationData;
    }

    private static CraftStationInteractionProfile ResolveProfileForFurniture(string prefabName)
    {
        if (CraftStationInteractionProfileRegistry.TryGetProfileForPrefab(prefabName, out var profile))
        {
            return profile;
        }

        return CraftStationInteractionProfileRegistry.GetDefaultProfile();
    }

    private static CraftStationInteractionProfile ResolveProfileForRestoredStation(RegisteredCraftStationSaveData saveData, string prefabName)
    {
        if (CraftStationInteractionProfileRegistry.TryGetProfileById(saveData.InteractionProfileId, out var savedProfile))
        {
            return savedProfile;
        }

        return ResolveProfileForFurniture(prefabName);
    }

    private RegisteredCraftStationData? FindCraftStationByFurniture(GameObject furnitureRoot)
    {
        return _craftStations.FirstOrDefault(candidate => candidate.FurnitureRoot != null && candidate.FurnitureRoot == furnitureRoot);
    }

    private static bool TryGetCraftStationAtCrosshair(out GameObject furnitureRoot, out CraftingStation craftingStation)
    {
        var activeCamera = Camera.main;
        if (activeCamera != null)
        {
            var ray = new Ray(activeCamera.transform.position, activeCamera.transform.forward);
            if (Physics.Raycast(ray, out var hitInfo, 100f, ~0, QueryTriggerInteraction.Ignore))
            {
                var station = hitInfo.collider.GetComponentInParent<CraftingStation>();
                if (station != null)
                {
                    furnitureRoot = station.gameObject;
                    craftingStation = station;
                    return true;
                }
            }
        }

        furnitureRoot = null!;
        craftingStation = null!;
        return false;
    }

    private static Vector3 GetReferencePosition(CraftingStation craftingStation)
    {
        return craftingStation.transform.position;
    }

    private static string BuildPersistentFurnitureId(CraftingStation craftingStation)
    {
        var nview = craftingStation.GetComponentInParent<ZNetView>();
        var referenceTransform = craftingStation.transform;

        if (nview != null && nview.GetZDO() != null)
        {
            var localPosition = nview.transform.InverseTransformPoint(referenceTransform.position);
            return $"zdo:{nview.GetZDO().m_uid}:craft:{craftingStation.gameObject.name}:{localPosition.x:0.000}:{localPosition.y:0.000}:{localPosition.z:0.000}";
        }

        var worldPosition = referenceTransform.position;
        return $"fallback:{craftingStation.gameObject.name}:{worldPosition.x:0.000}:{worldPosition.y:0.000}:{worldPosition.z:0.000}";
    }
}
