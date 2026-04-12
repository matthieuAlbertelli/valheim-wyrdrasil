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
        var fallbackPosition = saveData.UsePosition.ToVector3();
        var allStations = Object.FindObjectsByType<CraftingStation>(FindObjectsSortMode.None);

        var exactCandidates = allStations
            .Select(station => new
            {
                Station = station,
                PersistentId = BuildPersistentFurnitureId(station),
                Distance = Vector3.Distance(GetReferencePosition(station), fallbackPosition)
            })
            .Where(candidate => candidate.PersistentId == saveData.PersistentFurnitureId)
            .OrderBy(candidate => candidate.Distance)
            .ToList();

        if (exactCandidates.Count > 0)
        {
            var exactMatch = exactCandidates[0];
            if (exactMatch.Distance <= ResolveStationDistance)
            {
                craftStationData = new RegisteredCraftStationData(
                    saveData.Id,
                    saveData.BuildingId,
                    saveData.ZoneId,
                    saveData.DisplayName,
                    exactMatch.PersistentId,
                    saveData.ApproachPosition.ToVector3(),
                    saveData.UsePosition.ToVector3(),
                    saveData.UseForward.ToVector3());
                craftStationData.UpdateRuntimeBinding(exactMatch.Station.gameObject, exactMatch.Station);
                EnsureMarker(craftStationData);
                EnsureAnchorIndicator(craftStationData);
                UpdateMarker(craftStationData);
                UpdateAnchorIndicator(craftStationData);
                return true;
            }
        }

        var fallbackStation = allStations
            .OrderBy(station => Vector3.Distance(GetReferencePosition(station), fallbackPosition))
            .FirstOrDefault();

        if (fallbackStation != null)
        {
            var fallbackDistance = Vector3.Distance(GetReferencePosition(fallbackStation), fallbackPosition);
            if (fallbackDistance <= ResolveStationDistance)
            {
                craftStationData = new RegisteredCraftStationData(
                    saveData.Id,
                    saveData.BuildingId,
                    saveData.ZoneId,
                    saveData.DisplayName,
                    BuildPersistentFurnitureId(fallbackStation),
                    saveData.ApproachPosition.ToVector3(),
                    saveData.UsePosition.ToVector3(),
                    saveData.UseForward.ToVector3());
                craftStationData.UpdateRuntimeBinding(fallbackStation.gameObject, fallbackStation);
                EnsureMarker(craftStationData);
                EnsureAnchorIndicator(craftStationData);
                UpdateMarker(craftStationData);
                UpdateAnchorIndicator(craftStationData);
                return true;
            }
        }

        craftStationData = null!;
        return false;
    }

    public void DesignateCraftStationAtCrosshair()
    {
        if (!TryGetCraftStationAtCrosshair(out var furnitureRoot, out var craftingStation))
        {
            _log.LogWarning("Cannot designate craft station: the targeted object is not a valid Valheim crafting station.");
            return;
        }

        if (FindCraftStationByFurniture(furnitureRoot) != null)
        {
            _log.LogWarning("Cannot designate craft station: this furniture is already registered as a craft station.");
            return;
        }

        var zone = _zoneService.FindZoneContainingPointHorizontally(GetReferencePosition(craftingStation));
        if (zone == null)
        {
            _log.LogWarning("Cannot designate craft station yet: no functional zone footprint was found at the targeted table.");
            return;
        }

        var persistentFurnitureId = BuildPersistentFurnitureId(craftingStation);
        var data = new RegisteredCraftStationData(
            _nextCraftStationId++,
            zone.BuildingId,
            zone.Id,
            furnitureRoot.name,
            persistentFurnitureId,
            Vector3.zero,
            Vector3.zero,
            Vector3.zero);

        data.UpdateRuntimeBinding(furnitureRoot, craftingStation);
        ApplyKnownAnchorProfileIfAny(data);
        _craftStations.Add(data);
        EnsureMarker(data);
        EnsureAnchorIndicator(data);
        UpdateMarker(data);
        UpdateAnchorIndicator(data);
        _log.LogInfo($"Designated craft station #{data.Id} on '{data.DisplayName}' in zone #{zone.Id} (building #{zone.BuildingId}) with persistentId='{persistentFurnitureId}'.");
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

        var approach = CreateIndicatorPrimitive(root.transform, PrimitiveType.Sphere, "Approach", new Vector3(0.22f, 0.22f, 0.22f), new Color(1f, 0.75f, 0.2f, 1f));
        var use = CreateIndicatorPrimitive(root.transform, PrimitiveType.Sphere, "Use", new Vector3(0.16f, 0.16f, 0.16f), new Color(0.25f, 1f, 0.35f, 1f));
        var shaft = CreateIndicatorPrimitive(root.transform, PrimitiveType.Cube, "ArrowShaft", new Vector3(0.08f, 0.08f, 0.65f), new Color(1f, 0.25f, 0.25f, 1f));
        var head = CreateIndicatorPrimitive(root.transform, PrimitiveType.Cube, "ArrowHead", new Vector3(0.18f, 0.18f, 0.18f), new Color(1f, 0.25f, 0.25f, 1f));

        approach.transform.localPosition = Vector3.zero;
        use.transform.localPosition = Vector3.zero;
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

        root.transform.position = station.ApproachPosition + Vector3.up * 0.05f;
        var forward = station.UseForward;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = Vector3.forward;
        }

        root.transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);

        var use = root.transform.Find("Use");
        if (use != null)
        {
            use.position = station.UsePosition + Vector3.up * 0.08f;
        }
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

    private void ApplyKnownAnchorProfileIfAny(RegisteredCraftStationData station)
    {
        if (station.FurnitureRoot == null)
        {
            return;
        }

        var prefabName = station.FurnitureRoot.name;
        if (!CraftStationAnchorProfileRegistry.TryGetProfile(prefabName, out var profile))
        {
            return;
        }

        var root = station.FurnitureRoot.transform;
        var worldApproach = root.TransformPoint(profile.LocalApproachPosition);
        var worldUse = root.TransformPoint(profile.LocalUsePosition);
        var worldForward = root.TransformDirection(profile.LocalUseForward);
        worldForward.y = 0f;
        worldForward = worldForward.sqrMagnitude > 0.0001f ? worldForward.normalized : Vector3.forward;

        station.SetManualAnchor(worldApproach, worldUse, worldForward);

        _log.LogInfo($"Applied hardcoded craft station anchor profile '{profile.PrefabName}' to station #{station.Id}. localApproach={profile.LocalApproachPosition} localUse={profile.LocalUsePosition} localForward={profile.LocalUseForward}");
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
