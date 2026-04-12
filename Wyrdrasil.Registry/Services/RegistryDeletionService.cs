using BepInEx.Logging;
using Wyrdrasil.Settlements.Services;

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

    public RegistryDeletionService(
        ManualLogSource log,
        BuildingService buildingService,
        FunctionalZoneService zoneService,
        ZoneSlotService slotService,
        SeatService seatService,
        BedService bedService,
        CraftStationService craftStationService,
        NavigationWaypointService waypointService,
        RegistryResidentService residentService)
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
                _buildingService.DeleteBuildingIfUnused(deletedZone.BuildingId, _zoneService.Zones, _slotService.Slots, _seatService.Seats);
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
            _buildingService.DeleteBuildingIfUnused(slot.BuildingId, _zoneService.Zones, _slotService.Slots, _seatService.Seats);
            _log.LogInfo($"Deleted slot #{slot.Id}.");
        }
    }

    public void DeleteDesignatedSeatAtCrosshair()
    {
        if (_seatService.DeleteSeatAtCrosshair(out var seatId))
        {
            _residentService.HandleDeletedSeat(seatId);
            _log.LogInfo($"Deleted designated seat #{seatId}.");
            return;
        }

        _log.LogWarning("Cannot delete designated seat: no registered seat furniture was found under the crosshair.");
    }

    public void DeleteDesignatedBedAtCrosshair()
    {
        if (_bedService.DeleteBedAtCrosshair(out var bedId))
        {
            _residentService.HandleDeletedBed(bedId);
            _log.LogInfo($"Deleted designated bed #{bedId}.");
            return;
        }

        _log.LogWarning("Cannot delete designated bed: no registered bed furniture was found under the crosshair.");
    }

    public void DeleteDesignatedCraftStationAtCrosshair()
    {
        if (_craftStationService.DeleteCraftStationAtCrosshair(out var craftStationId))
        {
            _residentService.HandleDeletedCraftStation(craftStationId);
            _log.LogInfo($"Deleted designated craft station #{craftStationId}.");
            return;
        }

        _log.LogWarning("Cannot delete designated craft station: no registered craft station furniture was found under the crosshair.");
    }

    public void DeleteNavigationWaypointAtCrosshair()
    {
        if (_waypointService.DeleteWaypointAtCrosshair())
        {
            return;
        }

        _log.LogWarning("Cannot delete waypoint: no navigation waypoint was found under the crosshair.");
    }
}
