using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using UnityEngine;
using Wyrdrasil.Registry.Tool;
using Wyrdrasil.Core.Services;
using Wyrdrasil.Settlements.Tool;
using Wyrdrasil.Settlements.Components;

namespace Wyrdrasil.Settlements.Services;


public sealed class RegistryBedService
{
    private const float ResolveBedDistance = 2.5f;

    private readonly ManualLogSource _log;
    private readonly RegistryZoneService _zoneService;
    private readonly RegistryAnchorPolicyService _anchorPolicyService;
    private readonly List<RegisteredBedData> _beds = new();
    private readonly Dictionary<int, WyrdrasilRegisteredBedMarker> _markers = new();
    private readonly Dictionary<int, GameObject> _bedRoots = new();

    private int _nextBedId = 1;
    private bool _visualsVisible;
    private int? _pendingForceAssignTargetBedId;

    public IReadOnlyList<RegisteredBedData> Beds => _beds;
    public int NextBedId => _nextBedId;

    public RegistryBedService(
        ManualLogSource log,
        RegistryModeService modeService,
        RegistryZoneService zoneService,
        RegistryAnchorPolicyService anchorPolicyService)
    {
        _log = log;
        _zoneService = zoneService;
        _anchorPolicyService = anchorPolicyService;
        _visualsVisible = modeService.IsRegistryModeEnabled;
        modeService.RegistryModeChanged += OnRegistryModeChanged;
    }

    public void LoadBeds(IEnumerable<RegisteredBedData> beds, int nextBedId)
    {
        foreach (var bed in _beds)
        {
            if (_markers.TryGetValue(bed.Id, out var marker) && marker != null)
            {
                marker.SetVisualizationVisible(false, false);
            }
        }

        _beds.Clear();
        _markers.Clear();
        _bedRoots.Clear();

        foreach (var bed in beds)
        {
            _beds.Add(bed);
            if (bed.FurnitureRoot != null)
            {
                _bedRoots[bed.Id] = bed.FurnitureRoot;
                EnsureMarker(bed);
            }
        }

        _nextBedId = nextBedId;
    }

    public void ClearAllBeds()
    {
        foreach (var bed in _beds)
        {
            if (_markers.TryGetValue(bed.Id, out var marker) && marker != null)
            {
                marker.SetVisualizationVisible(false, false);
            }
        }

        _beds.Clear();
        _markers.Clear();
        _bedRoots.Clear();
        _pendingForceAssignTargetBedId = null;
        _nextBedId = 1;
    }

    public bool TryRestoreAssignment(int bedId, int residentId)
    {
        var bed = _beds.FirstOrDefault(candidate => candidate.Id == bedId);
        if (bed == null)
        {
            return false;
        }

        bed.AssignRegisteredNpc(residentId);
        UpdateMarker(bed);
        return true;
    }

    public bool TryGetBedById(int bedId, out RegisteredBedData bedData)
    {
        var bed = _beds.FirstOrDefault(candidate => candidate.Id == bedId);
        if (bed == null)
        {
            bedData = null!;
            return false;
        }

        bedData = bed;
        return true;
    }

    public bool TryResolveBedFromSave(RegisteredBedSaveData saveData, out RegisteredBedData bedData)
    {
        var fallbackPosition = saveData.SleepPosition.ToVector3();
        var allBeds = UnityEngine.Object.FindObjectsByType<Bed>(FindObjectsSortMode.None);

        var exactCandidates = allBeds
            .Select(bed => new
            {
                Bed = bed,
                PersistentId = BuildPersistentFurnitureId(bed),
                Distance = Vector3.Distance(GetBedReferencePosition(bed), fallbackPosition)
            })
            .Where(candidate => candidate.PersistentId == saveData.PersistentFurnitureId)
            .OrderBy(candidate => candidate.Distance)
            .ToList();

        if (exactCandidates.Count > 0)
        {
            var exactMatch = exactCandidates[0];
            if (exactMatch.Distance <= ResolveBedDistance)
            {
                var root = exactMatch.Bed.gameObject;
                bedData = new RegisteredBedData(
                    saveData.Id,
                    saveData.BuildingId,
                    saveData.ZoneId,
                    saveData.DisplayName,
                    exactMatch.PersistentId,
                    root,
                    exactMatch.Bed);
                return true;
            }
        }

        var fallbackBed = allBeds
            .OrderBy(bed => Vector3.Distance(GetBedReferencePosition(bed), fallbackPosition))
            .FirstOrDefault();

        if (fallbackBed != null)
        {
            var fallbackDistance = Vector3.Distance(GetBedReferencePosition(fallbackBed), fallbackPosition);
            if (fallbackDistance <= ResolveBedDistance)
            {
                var root = fallbackBed.gameObject;
                var persistentId = BuildPersistentFurnitureId(fallbackBed);
                bedData = new RegisteredBedData(
                    saveData.Id,
                    saveData.BuildingId,
                    saveData.ZoneId,
                    saveData.DisplayName,
                    persistentId,
                    root,
                    fallbackBed);
                return true;
            }
        }

        bedData = null!;
        return false;
    }

    public void DesignateBedAtCrosshair()
    {
        if (!TryGetBedAtCrosshair(out var furnitureRoot, out var bedComponent))
        {
            _log.LogWarning("Cannot designate bed: the targeted object is not a valid Valheim bed piece.");
            return;
        }

        if (FindBedByFurniture(furnitureRoot) != null)
        {
            _log.LogWarning("Cannot designate bed: this furniture is already registered as a bed.");
            return;
        }

        var referencePosition = GetBedReferencePosition(bedComponent);
        var zone = _zoneService.FindZoneContainingPointHorizontally(referencePosition);

        if (_anchorPolicyService.RequiresZoneForBed() && zone == null)
        {
            _log.LogWarning("Cannot designate bed: this bed requires a functional zone, but no zone was found.");
            return;
        }

        if (zone == null)
        {
            _log.LogWarning("Cannot designate bed: no compatible bedroom-style zone was found.");
            return;
        }

        if (!_anchorPolicyService.IsZoneTypeAllowedForBed(zone.ZoneType))
        {
            _log.LogWarning($"Cannot designate bed: zone type '{zone.ZoneType}' is not compatible with beds.");
            return;
        }

        var persistentFurnitureId = BuildPersistentFurnitureId(bedComponent);
        var bedData = new RegisteredBedData(
            _nextBedId++,
            zone.BuildingId,
            zone.Id,
            furnitureRoot.name,
            persistentFurnitureId,
            furnitureRoot,
            bedComponent);

        _beds.Add(bedData);
        _bedRoots[bedData.Id] = furnitureRoot;
        EnsureMarker(bedData);

        _log.LogInfo($"Designated bed #{bedData.Id} on bed '{bedData.DisplayName}' in zone #{zone.Id} (building #{zone.BuildingId}) with persistentId='{persistentFurnitureId}'.");
    }

    public bool TryGetBedAtCrosshair(out RegisteredBedData bedData)
    {
        if (!TryGetBedAtCrosshair(out var furnitureRoot, out _))
        {
            bedData = null!;
            return false;
        }

        var existingBed = FindBedByFurniture(furnitureRoot);
        if (existingBed == null)
        {
            bedData = null!;
            return false;
        }

        bedData = existingBed;
        return true;
    }

    public void SetPendingForceAssignTarget(int? bedId)
    {
        if (_pendingForceAssignTargetBedId == bedId)
        {
            return;
        }

        _pendingForceAssignTargetBedId = bedId;
        foreach (var bed in _beds)
        {
            UpdateMarker(bed);
        }
    }

    public bool TryAssignBed(int registeredNpcId, out RegisteredBedData? bedData)
    {
        foreach (var bed in _beds)
        {
            if (bed.AssignedRegisteredNpcId.HasValue)
            {
                continue;
            }

            bed.AssignRegisteredNpc(registeredNpcId);
            UpdateMarker(bed);
            bedData = bed;
            return true;
        }

        bedData = null;
        return false;
    }

    public bool ClearBedAssignment(int bedId, out int? previousResidentId)
    {
        foreach (var bed in _beds)
        {
            if (bed.Id != bedId)
            {
                continue;
            }

            previousResidentId = bed.AssignedRegisteredNpcId;
            if (!previousResidentId.HasValue)
            {
                return false;
            }

            bed.ClearAssignedRegisteredNpc();
            UpdateMarker(bed);
            return true;
        }

        previousResidentId = null;
        return false;
    }

    public bool ForceAssignBed(int bedId, int registeredNpcId, out int? previousResidentId, out RegisteredBedData? bedData)
    {
        foreach (var bed in _beds)
        {
            if (bed.Id != bedId)
            {
                continue;
            }

            previousResidentId = bed.AssignedRegisteredNpcId;
            bed.AssignRegisteredNpc(registeredNpcId);
            UpdateMarker(bed);
            bedData = bed;
            return true;
        }

        previousResidentId = null;
        bedData = null;
        return false;
    }

    public void ClearAssignmentForResident(int registeredNpcId)
    {
        foreach (var bed in _beds.Where(candidate => candidate.AssignedRegisteredNpcId == registeredNpcId))
        {
            bed.ClearAssignedRegisteredNpc();
            UpdateMarker(bed);
        }
    }

    public bool DeleteBedAtCrosshair(out int bedId)
    {
        bedId = 0;
        if (!TryGetBedAtCrosshair(out var furnitureRoot, out _))
        {
            return false;
        }

        var existingBed = FindBedByFurniture(furnitureRoot);
        if (existingBed == null)
        {
            return false;
        }

        bedId = existingBed.Id;
        return DeleteBed(existingBed.Id);
    }

    public IReadOnlyList<int> DeleteBedsInZone(int zoneId)
    {
        var bedIds = new List<int>();
        foreach (var bed in new List<RegisteredBedData>(_beds))
        {
            if (bed.ZoneId != zoneId)
            {
                continue;
            }

            bedIds.Add(bed.Id);
            DeleteBed(bed.Id);
        }

        return bedIds;
    }

    public bool DeleteBed(int bedId)
    {
        var index = _beds.FindIndex(bed => bed.Id == bedId);
        if (index < 0)
        {
            return false;
        }

        var bed = _beds[index];
        _beds.RemoveAt(index);

        if (_markers.TryGetValue(bedId, out var marker) && marker != null)
        {
            marker.SetVisualizationVisible(false, false);
        }

        _markers.Remove(bedId);
        _bedRoots.Remove(bedId);
        return true;
    }

    private void OnRegistryModeChanged(bool isEnabled)
    {
        _visualsVisible = isEnabled;
        foreach (var bed in _beds)
        {
            UpdateMarker(bed);
        }
    }

    private void EnsureMarker(RegisteredBedData bed)
    {
        if (bed.FurnitureRoot == null)
        {
            return;
        }

        var marker = bed.FurnitureRoot.GetComponent<WyrdrasilRegisteredBedMarker>() ?? bed.FurnitureRoot.AddComponent<WyrdrasilRegisteredBedMarker>();
        marker.Initialize(bed.Id);
        marker.RegisterRenderers(bed.FurnitureRoot.GetComponentsInChildren<Renderer>(true));
        marker.SetVisualizationVisible(_visualsVisible, bed.AssignedRegisteredNpcId.HasValue);
        marker.SetPendingForceAssignTarget(_pendingForceAssignTargetBedId == bed.Id);
        _markers[bed.Id] = marker;
    }

    private void UpdateMarker(RegisteredBedData bed)
    {
        if (_markers.TryGetValue(bed.Id, out var marker))
        {
            marker.SetVisualizationVisible(_visualsVisible, bed.AssignedRegisteredNpcId.HasValue);
            marker.SetPendingForceAssignTarget(_pendingForceAssignTargetBedId == bed.Id);
        }
    }

    private RegisteredBedData? FindBedByFurniture(GameObject furnitureRoot)
    {
        return _beds.FirstOrDefault(bed => bed.FurnitureRoot != null && bed.FurnitureRoot == furnitureRoot);
    }

    private static bool TryGetBedAtCrosshair(out GameObject furnitureRoot, out Bed bedComponent)
    {
        var activeCamera = Camera.main;
        if (activeCamera != null)
        {
            var ray = new Ray(activeCamera.transform.position, activeCamera.transform.forward);
            if (Physics.Raycast(ray, out var hitInfo, 100f, ~0, QueryTriggerInteraction.Ignore))
            {
                var bed = hitInfo.collider.GetComponentInParent<Bed>();
                if (bed != null)
                {
                    furnitureRoot = bed.gameObject;
                    bedComponent = bed;
                    return true;
                }
            }
        }

        furnitureRoot = null!;
        bedComponent = null!;
        return false;
    }

    private static Vector3 GetBedReferencePosition(Bed bed)
    {
        return bed.transform.position;
    }

    private static string BuildPersistentFurnitureId(Bed bed)
    {
        var nview = bed.GetComponentInParent<ZNetView>();
        var referenceTransform = bed.transform;

        if (nview != null && nview.GetZDO() != null)
        {
            var localPosition = nview.transform.InverseTransformPoint(referenceTransform.position);
            return $"zdo:{nview.GetZDO().m_uid}:bed:{bed.gameObject.name}:{localPosition.x:0.000}:{localPosition.y:0.000}:{localPosition.z:0.000}";
        }

        var worldPosition = referenceTransform.position;
        return $"fallback:{bed.gameObject.name}:{worldPosition.x:0.000}:{worldPosition.y:0.000}:{worldPosition.z:0.000}";
    }
}
