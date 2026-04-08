using BepInEx.Logging;
using Wyrdrasil.Settlements.Services;

namespace Wyrdrasil.Registry.Services;

public sealed class RegistryFlushService
{
    private readonly ManualLogSource _log;
    private readonly RegistryBuildingService _buildingService;
    private readonly RegistryZoneService _zoneService;
    private readonly RegistrySlotService _slotService;
    private readonly RegistrySeatService _seatService;
    private readonly RegistryWaypointService _waypointService;
    private readonly RegistryResidentService _residentService;
    private readonly RegistryPersistenceService _persistenceService;

    public RegistryFlushService(
        ManualLogSource log,
        RegistryBuildingService buildingService,
        RegistryZoneService zoneService,
        RegistrySlotService slotService,
        RegistrySeatService seatService,
        RegistryWaypointService waypointService,
        RegistryResidentService residentService,
        RegistryPersistenceService persistenceService)
    {
        _log = log;
        _buildingService = buildingService;
        _zoneService = zoneService;
        _slotService = slotService;
        _seatService = seatService;
        _waypointService = waypointService;
        _residentService = residentService;
        _persistenceService = persistenceService;
    }

    public void FlushAllRegistryState()
    {
        _residentService.ClearAllResidents();
        _waypointService.ClearAllWaypoints();
        _seatService.ClearAllSeats();
        _slotService.ClearAllSlots();
        _zoneService.ClearAllZones();
        _buildingService.ClearAllBuildings();
        _persistenceService.DeleteCurrentWorldSave();
        _log.LogInfo("Flushed all in-memory registry state. The next save will write an empty registry state for the current world.");
    }
}
