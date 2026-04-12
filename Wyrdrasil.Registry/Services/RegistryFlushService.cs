using BepInEx.Logging;
using Wyrdrasil.Settlements.Services;

namespace Wyrdrasil.Registry.Services;

public sealed class RegistryFlushService
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
    private readonly RegistryPersistenceService _persistenceService;

    public RegistryFlushService(
        ManualLogSource log,
        BuildingService buildingService,
        FunctionalZoneService zoneService,
        ZoneSlotService slotService,
        SeatService seatService,
        BedService bedService,
        CraftStationService craftStationService,
        NavigationWaypointService waypointService,
        RegistryResidentService residentService,
        RegistryPersistenceService persistenceService)
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
        _persistenceService = persistenceService;
    }

    public void FlushAllRegistryState()
    {
        _residentService.ClearAllResidents();
        _waypointService.ClearAllWaypoints();
        _seatService.ClearAllSeats();
        _bedService.ClearAllBeds();
        _craftStationService.ClearAllCraftStations();
        _slotService.ClearAllSlots();
        _zoneService.ClearAllZones();
        _buildingService.ClearAllBuildings();
        _persistenceService.DeleteCurrentWorldSave();
        _log.LogInfo("Flushed all in-memory registry state. The next save will write an empty registry state for the current world.");
    }
}
