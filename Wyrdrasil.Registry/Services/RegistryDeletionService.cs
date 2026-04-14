using System.Collections.Generic;
using BepInEx.Logging;
using Wyrdrasil.Construction.Testing;
using Wyrdrasil.Settlements.Services;
using Wyrdrasil.Settlements.Tool;

namespace Wyrdrasil.Registry.Services;

public sealed class RegistryDeletionService
{
    private readonly ManualLogSource _log;
    private readonly BuildingService _buildingService;
    private readonly FunctionalZoneService _zoneService;
    private readonly ZoneSlotService _slotService;
    private readonly SeatService _seatService;
    private readonly BedService _bedService;
    private readonly CraftStationService _craftStationService;
    private readonly NavigationWaypointService _waypointService;
    private readonly RegistryResidentService _residentService;
    private readonly IConstructionTestingApi _constructionTestingApi;

    public RegistryDeletionService(
        ManualLogSource log,
        BuildingService buildingService,
        FunctionalZoneService zoneService,
        ZoneSlotService slotService,
        SeatService seatService,
        BedService bedService,
        CraftStationService craftStationService,
        NavigationWaypointService waypointService,
        RegistryResidentService residentService,
        IConstructionTestingApi constructionTestingApi)
    {
        _log = log;
        _buildingService = buildingService;
        _zoneService = zoneService;
        _slotService = slotService;
        _seatService = seatService;
        _bedService = bedService;
        _craftStationService = craftStationService;
        _waypointService = waypointService;
        _residentService = residentService;
        _constructionTestingApi = constructionTestingApi;
    }

    public void DeleteZoneAtCrosshair()
    {
        if (!_zoneService.TryGetPlacementPoint(out var point) || !_zoneService.TryFindZoneAtPoint(point, out var zone))
        {
            _log.LogWarning("Cannot delete zone: no functional zone was found under the crosshair.");
            return;
        }

        var deletedSlotIds = _slotService.DeleteSlotsInZone(zone.Id);
        foreach (var slotId in deletedSlotIds)
        {
            _residentService.HandleDeletedSlot(slotId);
        }

        var deletedSeatIds = _seatService.DeleteSeatsInZone(zone.Id);
        foreach (var seatId in deletedSeatIds)
        {
            _residentService.HandleDeletedSeat(seatId);
        }

        var deletedBedIds = _bedService.DeleteBedsInZone(zone.Id);
        foreach (var bedId in deletedBedIds)
        {
            _residentService.HandleDeletedBed(bedId);
        }

        var deletedCraftStationIds = _craftStationService.DeleteCraftStationsInZone(zone.Id);
        foreach (var craftStationId in deletedCraftStationIds)
        {
            _residentService.HandleDeletedCraftStation(craftStationId);
        }

        if (_zoneService.DeleteZone(zone.Id, out var deletedZone))
        {
            if (deletedZone != null)
            {
                _buildingService.DeleteBuildingIfUnused(deletedZone.BuildingId, _zoneService.Zones, _slotService.Slots, _seatService.Seats, _bedService.Beds, _craftStationService.CraftStations);
            }

            _log.LogInfo($"Deleted zone #{zone.Id}.");
        }
    }

    public void DeleteSlotAtCrosshair()
    {
        if (!_slotService.TryGetPlacementPoint(out var point) || !_slotService.TryFindSlotAtPoint(point, out var slot))
        {
            _log.LogWarning("Cannot delete slot: no innkeeper slot was found under the crosshair.");
            return;
        }

        if (_slotService.DeleteSlot(slot.Id))
        {
            _residentService.HandleDeletedSlot(slot.Id);
            _buildingService.DeleteBuildingIfUnused(slot.BuildingId, _zoneService.Zones, _slotService.Slots, _seatService.Seats, _bedService.Beds, _craftStationService.CraftStations);
            _log.LogInfo($"Deleted slot #{slot.Id}.");
        }
    }

    public void DeleteDesignatedSeatAtCrosshair()
    {
        if (!_seatService.TryGetSeatAtCrosshair(out var seat))
        {
            _log.LogWarning("Cannot delete designated seat: no registered seat furniture was found under the crosshair.");
            return;
        }

        if (_seatService.DeleteSeat(seat.Id))
        {
            _residentService.HandleDeletedSeat(seat.Id);
            _buildingService.DeleteBuildingIfUnused(seat.BuildingId, _zoneService.Zones, _slotService.Slots, _seatService.Seats, _bedService.Beds, _craftStationService.CraftStations);
            _log.LogInfo($"Deleted designated seat #{seat.Id}.");
        }
    }

    public void DeleteDesignatedBedAtCrosshair()
    {
        if (!_bedService.TryGetBedAtCrosshair(out var bed))
        {
            _log.LogWarning("Cannot delete designated bed: no registered bed furniture was found under the crosshair.");
            return;
        }

        if (_bedService.DeleteBed(bed.Id))
        {
            _residentService.HandleDeletedBed(bed.Id);
            _buildingService.DeleteBuildingIfUnused(bed.BuildingId, _zoneService.Zones, _slotService.Slots, _seatService.Seats, _bedService.Beds, _craftStationService.CraftStations);
            _log.LogInfo($"Deleted designated bed #{bed.Id}.");
        }
    }

    public void DeleteDesignatedCraftStationAtCrosshair()
    {
        if (!_craftStationService.TryGetCraftStationAtCrosshair(out var craftStation))
        {
            _log.LogWarning("Cannot delete designated craft station: no registered craft station furniture was found under the crosshair.");
            return;
        }

        if (_craftStationService.DeleteCraftStation(craftStation.Id))
        {
            _residentService.HandleDeletedCraftStation(craftStation.Id);
            _buildingService.DeleteBuildingIfUnused(craftStation.BuildingId, _zoneService.Zones, _slotService.Slots, _seatService.Seats, _bedService.Beds, _craftStationService.CraftStations);
            _log.LogInfo($"Deleted designated craft station #{craftStation.Id}.");
        }
    }

    public void DeleteNavigationWaypointAtCrosshair()
    {
        if (_waypointService.DeleteWaypointAtCrosshair())
        {
            return;
        }

        _log.LogWarning("Cannot delete waypoint: no navigation waypoint was found under the crosshair.");
    }

    public void PurgeAllConstructionInTargetZone()
    {
        if (!_zoneService.TryGetPlacementPoint(out var point) || !_zoneService.TryFindZoneAtPoint(point, out var zone))
        {
            _log.LogWarning("Cannot purge construction: no functional zone was found under the crosshair.");
            return;
        }

        var seatIdsToDelete = FindSeatIdsIntersectingZone(zone);
        var bedIdsToDelete = FindBedIdsIntersectingZone(zone);
        var craftStationIdsToDelete = FindCraftStationIdsIntersectingZone(zone);

        if (!_constructionTestingApi.TryPurgeAllConstructionInZone(zone, out var report, out var failureReason))
        {
            _log.LogWarning(failureReason);
            return;
        }

        foreach (var seatId in seatIdsToDelete)
        {
            if (_seatService.DeleteSeat(seatId))
            {
                _residentService.HandleDeletedSeat(seatId);
            }
        }

        foreach (var bedId in bedIdsToDelete)
        {
            if (_bedService.DeleteBed(bedId))
            {
                _residentService.HandleDeletedBed(bedId);
            }
        }

        foreach (var craftStationId in craftStationIdsToDelete)
        {
            if (_craftStationService.DeleteCraftStation(craftStationId))
            {
                _residentService.HandleDeletedCraftStation(craftStationId);
            }
        }

        var clearedConstructionAssignments = _residentService.ClearStaleConstructionAssignments();
        _log.LogInfo($"Purged all construction in zone #{zone.Id}: destroyed {report.DestroyedPieceCount} piece instance(s), deleted {report.DeletedProjectCount} chantier project(s), removed {seatIdsToDelete.Count} seat designation(s), {bedIdsToDelete.Count} bed designation(s) and {craftStationIdsToDelete.Count} craft station designation(s). Cleared {clearedConstructionAssignments} stale chantier assignment(s).");
    }

    private List<int> FindSeatIdsIntersectingZone(FunctionalZoneData zone)
    {
        var ids = new List<int>();
        foreach (var seat in _seatService.Seats)
        {
            if (seat.FurnitureRoot == null || !ZoneVolumeOverlapUtility.TryGetWorldBounds(seat.FurnitureRoot, out var bounds))
            {
                continue;
            }

            if (ZoneVolumeOverlapUtility.IntersectsZone(zone, bounds))
            {
                ids.Add(seat.Id);
            }
        }

        return ids;
    }

    private List<int> FindBedIdsIntersectingZone(FunctionalZoneData zone)
    {
        var ids = new List<int>();
        foreach (var bed in _bedService.Beds)
        {
            if (bed.FurnitureRoot == null || !ZoneVolumeOverlapUtility.TryGetWorldBounds(bed.FurnitureRoot, out var bounds))
            {
                continue;
            }

            if (ZoneVolumeOverlapUtility.IntersectsZone(zone, bounds))
            {
                ids.Add(bed.Id);
            }
        }

        return ids;
    }

    private List<int> FindCraftStationIdsIntersectingZone(FunctionalZoneData zone)
    {
        var ids = new List<int>();
        foreach (var craftStation in _craftStationService.CraftStations)
        {
            if (craftStation.FurnitureRoot == null || !ZoneVolumeOverlapUtility.TryGetWorldBounds(craftStation.FurnitureRoot, out var bounds))
            {
                continue;
            }

            if (ZoneVolumeOverlapUtility.IntersectsZone(zone, bounds))
            {
                ids.Add(craftStation.Id);
            }
        }

        return ids;
    }
}
