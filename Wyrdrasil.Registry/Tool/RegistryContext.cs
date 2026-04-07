using BepInEx.Logging;
using Wyrdrasil.Registry.Services;

namespace Wyrdrasil.Registry.Tool;

public sealed class RegistryContext
{
    public ManualLogSource Log { get; }
    public RegistryBuildingService BuildingService { get; }
    public RegistryZoneService ZoneService { get; }
    public RegistryWaypointService WaypointService { get; }
    public RegistrySlotService SlotService { get; }
    public RegistrySeatService SeatService { get; }
    public RegistrySpawnService SpawnService { get; }
    public RegistryResidentService ResidentService { get; }
    public RegistryDiagnosticsService DiagnosticsService { get; }
    public RegistryDeletionService DeletionService { get; }

    public RegistryContext(
        ManualLogSource log,
        RegistryBuildingService buildingService,
        RegistryZoneService zoneService,
        RegistryWaypointService waypointService,
        RegistrySlotService slotService,
        RegistrySeatService seatService,
        RegistrySpawnService spawnService,
        RegistryResidentService residentService,
        RegistryDiagnosticsService diagnosticsService,
        RegistryDeletionService deletionService)
    {
        Log = log;
        BuildingService = buildingService;
        ZoneService = zoneService;
        WaypointService = waypointService;
        SlotService = slotService;
        SeatService = seatService;
        SpawnService = spawnService;
        ResidentService = residentService;
        DiagnosticsService = diagnosticsService;
        DeletionService = deletionService;
    }
}
