using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;
using Wyrdrasil.Registry.Components;
using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Services;

public sealed class RegistrySeatService
{
    private readonly ManualLogSource _log;
    private readonly RegistryZoneService _zoneService;
    private readonly List<RegisteredSeatData> _seats = new();
    private readonly Dictionary<int, WyrdrasilRegisteredSeatMarker> _markers = new();
    private readonly Dictionary<int, GameObject> _seatRoots = new();

    private int _nextSeatId = 1;
    private bool _visualsVisible;
    private int? _pendingForceAssignTargetSeatId;

    public IReadOnlyList<RegisteredSeatData> Seats => _seats;

    public RegistrySeatService(ManualLogSource log, RegistryModeService modeService, RegistryZoneService zoneService)
    {
        _log = log;
        _zoneService = zoneService;
        _visualsVisible = modeService.IsRegistryModeEnabled;
        modeService.RegistryModeChanged += OnRegistryModeChanged;
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

        var tavernZone = _zoneService.FindZoneContainingPoint(furnitureRoot.transform.position, ZoneType.Tavern);
        if (tavernZone == null)
        {
            _log.LogWarning("Cannot designate seat: the targeted furniture is not inside a Tavern zone.");
            return;
        }

        var seatData = new RegisteredSeatData(_nextSeatId++, tavernZone.Id, furnitureRoot.name, furnitureRoot, chairComponent);
        _seats.Add(seatData);
        _seatRoots[seatData.Id] = furnitureRoot;
        EnsureMarker(seatData);

        _log.LogInfo($"Designated seat #{seatData.Id} on chair '{seatData.DisplayName}' in Tavern zone #{seatData.ZoneId}.");
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
        foreach (var seat in _seats)
        {
            if (seat.AssignedRegisteredNpcId != registeredNpcId)
            {
                continue;
            }

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
        var marker = seat.FurnitureRoot.GetComponent<WyrdrasilRegisteredSeatMarker>();
        if (!marker)
        {
            marker = seat.FurnitureRoot.AddComponent<WyrdrasilRegisteredSeatMarker>();
        }

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
        foreach (var seat in _seats)
        {
            if (seat.FurnitureRoot == furnitureRoot)
            {
                return seat;
            }
        }

        return null;
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
}
