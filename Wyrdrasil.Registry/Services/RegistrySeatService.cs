using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using UnityEngine;
using Wyrdrasil.Registry.Components;
using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Services;

public sealed class RegistrySeatService
{
    private const float ResolveSeatDistance = 2f;

    private readonly ManualLogSource _log;
    private readonly RegistryZoneService _zoneService;
    private readonly RegistryAnchorPolicyService _anchorPolicyService;
    private readonly List<RegisteredSeatData> _seats = new();
    private readonly Dictionary<int, WyrdrasilRegisteredSeatMarker> _markers = new();
    private readonly Dictionary<int, GameObject> _seatRoots = new();

    private int _nextSeatId = 1;
    private bool _visualsVisible;
    private int? _pendingForceAssignTargetSeatId;

    public IReadOnlyList<RegisteredSeatData> Seats => _seats;
    public int NextSeatId => _nextSeatId;

    public RegistrySeatService(ManualLogSource log, RegistryModeService modeService, RegistryZoneService zoneService, RegistryAnchorPolicyService anchorPolicyService)
    {
        _log = log;
        _zoneService = zoneService;
        _anchorPolicyService = anchorPolicyService;
        _visualsVisible = modeService.IsRegistryModeEnabled;
        modeService.RegistryModeChanged += OnRegistryModeChanged;
    }

    public void LoadSeats(IEnumerable<RegisteredSeatData> seats, int nextSeatId)
    {
        foreach (var seat in _seats)
        {
            if (_markers.TryGetValue(seat.Id, out var marker) && marker != null)
            {
                marker.SetVisualizationVisible(false, false);
            }
        }

        _seats.Clear();
        _markers.Clear();
        _seatRoots.Clear();

        foreach (var seat in seats)
        {
            _seats.Add(seat);
            if (seat.FurnitureRoot != null)
            {
                _seatRoots[seat.Id] = seat.FurnitureRoot;
                EnsureMarker(seat);
            }
        }

        _nextSeatId = nextSeatId;
    }

    public void ClearAllSeats()
    {
        foreach (var seat in _seats)
        {
            if (_markers.TryGetValue(seat.Id, out var marker) && marker != null)
            {
                marker.SetVisualizationVisible(false, false);
            }
        }

        _seats.Clear();
        _markers.Clear();
        _seatRoots.Clear();
        _pendingForceAssignTargetSeatId = null;
        _nextSeatId = 1;
    }

    public bool TryRestoreAssignment(int seatId, int residentId)
    {
        var seat = _seats.FirstOrDefault(candidate => candidate.Id == seatId);
        if (seat == null)
        {
            return false;
        }

        seat.AssignRegisteredNpc(residentId);
        UpdateMarker(seat);
        return true;
    }

    public bool TryGetSeatById(int seatId, out RegisteredSeatData seatData)
    {
        var seat = _seats.FirstOrDefault(candidate => candidate.Id == seatId);
        if (seat == null)
        {
            seatData = null!;
            return false;
        }

        seatData = seat;
        return true;
    }

    public bool TryResolveSeatFromSave(RegisteredSeatSaveData saveData, out RegisteredSeatData seatData)
    {
        var fallbackPosition = saveData.SeatPosition.ToVector3();
        var allChairs = Object.FindObjectsByType<Chair>(FindObjectsSortMode.None);

        var exactCandidates = allChairs
            .Select(chair => new
            {
                Chair = chair,
                PersistentId = BuildPersistentFurnitureId(chair),
                Distance = Vector3.Distance(GetSeatReferencePosition(chair), fallbackPosition)
            })
            .Where(candidate => candidate.PersistentId == saveData.PersistentFurnitureId)
            .OrderBy(candidate => candidate.Distance)
            .ToList();

        if (exactCandidates.Count > 0)
        {
            var exactMatch = exactCandidates[0];
            if (exactMatch.Distance <= ResolveSeatDistance)
            {
                var root = exactMatch.Chair.gameObject;
                seatData = new RegisteredSeatData(
                    saveData.Id,
                    saveData.BuildingId,
                    saveData.ZoneId,
                    saveData.UsageType,
                    saveData.DisplayName,
                    exactMatch.PersistentId,
                    root,
                    exactMatch.Chair);
                return true;
            }
        }

        var fallbackChair = allChairs
            .OrderBy(chair => Vector3.Distance(GetSeatReferencePosition(chair), fallbackPosition))
            .FirstOrDefault();

        if (fallbackChair != null)
        {
            var fallbackDistance = Vector3.Distance(GetSeatReferencePosition(fallbackChair), fallbackPosition);
            if (fallbackDistance <= ResolveSeatDistance)
            {
                var root = fallbackChair.gameObject;
                var persistentId = BuildPersistentFurnitureId(fallbackChair);
                seatData = new RegisteredSeatData(
                    saveData.Id,
                    saveData.BuildingId,
                    saveData.ZoneId,
                    saveData.UsageType,
                    saveData.DisplayName,
                    persistentId,
                    root,
                    fallbackChair);
                return true;
            }
        }

        seatData = null!;
        return false;
    }

    public void DesignateSeatAtCrosshair()
    {
        if (!TryGetChairAtCrosshair(out var furnitureRoot, out var chairComponent))
        {
            _log.LogWarning("Cannot designate seat: the targeted object is not a valid Valheim chair piece.");
            return;
        }

        if (FindSeatByFurniture(furnitureRoot) != null)
        {
            _log.LogWarning("Cannot designate seat: this furniture is already registered as a seat.");
            return;
        }

        const SeatUsageType defaultUsageType = SeatUsageType.Public;
        var referencePosition = GetSeatReferencePosition(chairComponent);
        var zone = _zoneService.FindZoneContainingPointHorizontally(referencePosition);

        if (_anchorPolicyService.RequiresZone(defaultUsageType) && zone == null)
        {
            _log.LogWarning("Cannot designate seat: this seat usage requires a functional zone, but no zone was found.");
            return;
        }

        if (zone == null)
        {
            _log.LogWarning("Cannot designate seat yet: no functional zone footprint was found at the targeted chair, and standalone building assignment is not authored in this iteration.");
            return;
        }

        if (!_anchorPolicyService.IsZoneTypeAllowed(defaultUsageType, zone.ZoneType))
        {
            _log.LogWarning($"Cannot designate seat: zone type '{zone.ZoneType}' is not compatible with seat usage '{defaultUsageType}'.");
            return;
        }

        var persistentFurnitureId = BuildPersistentFurnitureId(chairComponent);
        var seatData = new RegisteredSeatData(
            _nextSeatId++,
            zone.BuildingId,
            zone.Id,
            defaultUsageType,
            furnitureRoot.name,
            persistentFurnitureId,
            furnitureRoot,
            chairComponent);

        _seats.Add(seatData);
        _seatRoots[seatData.Id] = furnitureRoot;
        EnsureMarker(seatData);

        _log.LogInfo($"Designated seat #{seatData.Id} on chair '{seatData.DisplayName}' in zone #{zone.Id} (building #{zone.BuildingId}) as {seatData.UsageType} with persistentId='{persistentFurnitureId}'.");
    }

    public bool TryGetSeatAtCrosshair(out RegisteredSeatData seatData)
    {
        if (!TryGetChairAtCrosshair(out var furnitureRoot, out _))
        {
            seatData = null!;
            return false;
        }

        var existingSeat = FindSeatByFurniture(furnitureRoot);
        if (existingSeat == null)
        {
            seatData = null!;
            return false;
        }

        seatData = existingSeat;
        return true;
    }

    public void SetPendingForceAssignTarget(int? seatId)
    {
        if (_pendingForceAssignTargetSeatId == seatId)
        {
            return;
        }

        _pendingForceAssignTargetSeatId = seatId;
        foreach (var seat in _seats)
        {
            UpdateMarker(seat);
        }
    }

    public bool TryAssignSeat(int registeredNpcId, out RegisteredSeatData? seatData)
    {
        foreach (var seat in _seats)
        {
            if (seat.AssignedRegisteredNpcId.HasValue)
            {
                continue;
            }

            seat.AssignRegisteredNpc(registeredNpcId);
            UpdateMarker(seat);
            seatData = seat;
            return true;
        }

        seatData = null;
        return false;
    }

    public bool ClearSeatAssignment(int seatId, out int? previousResidentId)
    {
        foreach (var seat in _seats)
        {
            if (seat.Id != seatId)
            {
                continue;
            }

            previousResidentId = seat.AssignedRegisteredNpcId;
            if (!previousResidentId.HasValue)
            {
                return false;
            }

            seat.ClearAssignedRegisteredNpc();
            UpdateMarker(seat);
            return true;
        }

        previousResidentId = null;
        return false;
    }

    public bool ForceAssignSeat(int seatId, int registeredNpcId, out int? previousResidentId, out RegisteredSeatData? seatData)
    {
        foreach (var seat in _seats)
        {
            if (seat.Id != seatId)
            {
                continue;
            }

            previousResidentId = seat.AssignedRegisteredNpcId;
            seat.AssignRegisteredNpc(registeredNpcId);
            UpdateMarker(seat);
            seatData = seat;
            return true;
        }

        previousResidentId = null;
        seatData = null;
        return false;
    }

    public void ClearAssignmentForResident(int registeredNpcId)
    {
        foreach (var seat in _seats.Where(seat => seat.AssignedRegisteredNpcId == registeredNpcId))
        {
            seat.ClearAssignedRegisteredNpc();
            UpdateMarker(seat);
        }
    }

    public bool DeleteSeatAtCrosshair(out int seatId)
    {
        seatId = 0;
        if (!TryGetChairAtCrosshair(out var furnitureRoot, out _))
        {
            return false;
        }

        var existingSeat = FindSeatByFurniture(furnitureRoot);
        if (existingSeat == null)
        {
            return false;
        }

        seatId = existingSeat.Id;
        return DeleteSeat(existingSeat.Id);
    }

    public IReadOnlyList<int> DeleteSeatsInZone(int zoneId)
    {
        var seatIds = new List<int>();
        foreach (var seat in new List<RegisteredSeatData>(_seats))
        {
            if (seat.ZoneId != zoneId)
            {
                continue;
            }

            seatIds.Add(seat.Id);
            DeleteSeat(seat.Id);
        }

        return seatIds;
    }

    public bool DeleteSeat(int seatId)
    {
        var index = _seats.FindIndex(seat => seat.Id == seatId);
        if (index < 0)
        {
            return false;
        }

        var seat = _seats[index];
        _seats.RemoveAt(index);

        if (_markers.TryGetValue(seatId, out var marker) && marker != null)
        {
            marker.SetVisualizationVisible(false, false);
        }

        _markers.Remove(seatId);
        _seatRoots.Remove(seatId);
        return true;
    }

    private void OnRegistryModeChanged(bool isEnabled)
    {
        _visualsVisible = isEnabled;
        foreach (var seat in _seats)
        {
            UpdateMarker(seat);
        }
    }

    private void EnsureMarker(RegisteredSeatData seat)
    {
        if (seat.FurnitureRoot == null)
        {
            return;
        }

        var marker = seat.FurnitureRoot.GetComponent<WyrdrasilRegisteredSeatMarker>() ?? seat.FurnitureRoot.AddComponent<WyrdrasilRegisteredSeatMarker>();
        marker.Initialize(seat.Id);
        marker.RegisterRenderers(seat.FurnitureRoot.GetComponentsInChildren<Renderer>(true));
        marker.SetVisualizationVisible(_visualsVisible, seat.AssignedRegisteredNpcId.HasValue);
        marker.SetPendingForceAssignTarget(_pendingForceAssignTargetSeatId == seat.Id);
        _markers[seat.Id] = marker;
    }

    private void UpdateMarker(RegisteredSeatData seat)
    {
        if (_markers.TryGetValue(seat.Id, out var marker))
        {
            marker.SetVisualizationVisible(_visualsVisible, seat.AssignedRegisteredNpcId.HasValue);
            marker.SetPendingForceAssignTarget(_pendingForceAssignTargetSeatId == seat.Id);
        }
    }

    private RegisteredSeatData? FindSeatByFurniture(GameObject furnitureRoot)
    {
        return _seats.FirstOrDefault(seat => seat.FurnitureRoot != null && seat.FurnitureRoot == furnitureRoot);
    }

    private static bool TryGetChairAtCrosshair(out GameObject furnitureRoot, out Chair chairComponent)
    {
        var activeCamera = Camera.main;
        if (activeCamera != null)
        {
            var ray = new Ray(activeCamera.transform.position, activeCamera.transform.forward);
            if (Physics.Raycast(ray, out var hitInfo, 100f, ~0, QueryTriggerInteraction.Ignore))
            {
                var chair = hitInfo.collider.GetComponentInParent<Chair>();
                if (chair != null)
                {
                    furnitureRoot = chair.gameObject;
                    chairComponent = chair;
                    return true;
                }
            }
        }

        furnitureRoot = null!;
        chairComponent = null!;
        return false;
    }

    private static Vector3 GetSeatReferencePosition(Chair chair)
    {
        return chair.m_attachPoint != null ? chair.m_attachPoint.position : chair.transform.position;
    }

    private static string BuildPersistentFurnitureId(Chair chair)
    {
        var nview = chair.GetComponentInParent<ZNetView>();
        var referenceTransform = chair.m_attachPoint != null ? chair.m_attachPoint : chair.transform;

        if (nview != null && nview.GetZDO() != null)
        {
            var localPosition = nview.transform.InverseTransformPoint(referenceTransform.position);
            return $"zdo:{nview.GetZDO().m_uid}:chair:{chair.gameObject.name}:{localPosition.x:0.000}:{localPosition.y:0.000}:{localPosition.z:0.000}";
        }

        var worldPosition = referenceTransform.position;
        return $"fallback:{chair.gameObject.name}:{worldPosition.x:0.000}:{worldPosition.y:0.000}:{worldPosition.z:0.000}";
    }
}
