using System.Collections.Generic;
using BepInEx.Logging;
using Wyrdrasil.Construction.Testing;
using Wyrdrasil.Settlements.Authoring;
using Wyrdrasil.Settlements.Runtime;
using Wyrdrasil.Settlements.Services;
using Wyrdrasil.Settlements.Tool;

namespace Wyrdrasil.Registry.Services;

public sealed class RegistryDeletionService
{
    private readonly ManualLogSource _log;
    private readonly ISettlementsAuthoringApi _settlementsAuthoringApi;
    private readonly ISettlementsRuntimeApi _settlementsRuntimeApi;
    private readonly SeatService _seatService;
    private readonly BedService _bedService;
    private readonly CraftStationService _craftStationService;
    private readonly RegistryResidentService _residentService;
    private readonly IConstructionTestingApi _constructionTestingApi;

    public RegistryDeletionService(
        ManualLogSource log,
        ISettlementsAuthoringApi settlementsAuthoringApi,
        ISettlementsRuntimeApi settlementsRuntimeApi,
        SeatService seatService,
        BedService bedService,
        CraftStationService craftStationService,
        RegistryResidentService residentService,
        IConstructionTestingApi constructionTestingApi)
    {
        _log = log;
        _settlementsAuthoringApi = settlementsAuthoringApi;
        _settlementsRuntimeApi = settlementsRuntimeApi;
        _seatService = seatService;
        _bedService = bedService;
        _craftStationService = craftStationService;
        _residentService = residentService;
        _constructionTestingApi = constructionTestingApi;
    }

    public void DeleteZoneAtCrosshair()
    {
        if (!_settlementsRuntimeApi.TryDeleteZoneAtCrosshair(out var report))
        {
            _log.LogWarning("Cannot delete zone: no functional zone was found under the crosshair.");
            return;
        }

        HandleResidentCleanup(report);
        if (report.DeletedZoneId.HasValue)
        {
            _log.LogInfo($"Deleted zone #{report.DeletedZoneId.Value}.");
        }
    }

    public void DeleteSlotAtCrosshair()
    {
        if (!_settlementsRuntimeApi.TryDeleteSlotAtCrosshair(out var report) || report.DeletedSlotIds.Count == 0)
        {
            _log.LogWarning("Cannot delete slot: no innkeeper slot was found under the crosshair.");
            return;
        }

        HandleResidentCleanup(report);
        _log.LogInfo($"Deleted slot #{report.DeletedSlotIds[0]}.");
    }

    public void DeleteDesignatedSeatAtCrosshair()
    {
        if (!_settlementsRuntimeApi.TryDeleteSeatAtCrosshair(out var report) || report.DeletedSeatIds.Count == 0)
        {
            _log.LogWarning("Cannot delete designated seat: no registered seat furniture was found under the crosshair.");
            return;
        }

        HandleResidentCleanup(report);
        _log.LogInfo($"Deleted designated seat #{report.DeletedSeatIds[0]}.");
    }

    public void DeleteDesignatedBedAtCrosshair()
    {
        if (!_settlementsRuntimeApi.TryDeleteBedAtCrosshair(out var report) || report.DeletedBedIds.Count == 0)
        {
            _log.LogWarning("Cannot delete designated bed: no registered bed furniture was found under the crosshair.");
            return;
        }

        HandleResidentCleanup(report);
        _log.LogInfo($"Deleted designated bed #{report.DeletedBedIds[0]}.");
    }

    public void DeleteDesignatedCraftStationAtCrosshair()
    {
        if (!_settlementsRuntimeApi.TryDeleteCraftStationAtCrosshair(out var report) || report.DeletedCraftStationIds.Count == 0)
        {
            _log.LogWarning("Cannot delete designated craft station: no registered craft station furniture was found under the crosshair.");
            return;
        }

        HandleResidentCleanup(report);
        _log.LogInfo($"Deleted designated craft station #{report.DeletedCraftStationIds[0]}.");
    }

    public void DeleteNavigationWaypointAtCrosshair()
    {
        if (_settlementsRuntimeApi.TryDeleteWaypointAtCrosshair())
        {
            return;
        }

        _log.LogWarning("Cannot delete waypoint: no navigation waypoint was found under the crosshair.");
    }

    public void PurgeAllConstructionInTargetZone()
    {
        if (!_settlementsAuthoringApi.TryGetPlacementPoint(out var point) || !_settlementsAuthoringApi.TryFindZoneAtPoint(point, out var zone))
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

    private void HandleResidentCleanup(SettlementsDeletionReport report)
    {
        foreach (var slotId in report.DeletedSlotIds)
        {
            _residentService.HandleDeletedSlot(slotId);
        }

        foreach (var seatId in report.DeletedSeatIds)
        {
            _residentService.HandleDeletedSeat(seatId);
        }

        foreach (var bedId in report.DeletedBedIds)
        {
            _residentService.HandleDeletedBed(bedId);
        }

        foreach (var craftStationId in report.DeletedCraftStationIds)
        {
            _residentService.HandleDeletedCraftStation(craftStationId);
        }
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
