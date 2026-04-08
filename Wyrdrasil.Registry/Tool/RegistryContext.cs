using BepInEx.Logging;
using Wyrdrasil.Registry.Services;
using Wyrdrasil.Routines.Services;
using Wyrdrasil.Settlements.Services;
using Wyrdrasil.Souls.Services;

namespace Wyrdrasil.Registry.Tool;

public sealed class RegistryContext
{
    public ManualLogSource Log { get; }
    public RegistryZoneService ZoneService { get; }
    public RegistryWaypointService WaypointService { get; }
    public RegistrySlotService SlotService { get; }
    public RegistrySeatService SeatService { get; }
    public RegistryBedService BedService { get; }
    public RegistrySpawnService SpawnService { get; }
    public RegistryResidentService ResidentService { get; }
    public RegistryDiagnosticsService DiagnosticsService { get; }
    public RegistryDeletionService DeletionService { get; }
    public RegistryFlushService FlushService { get; }
    public RegistryWorldClockService WorldClockService { get; }

    public RegistryContext(
        ManualLogSource log,
        RegistryZoneService zoneService,
        RegistryWaypointService waypointService,
        RegistrySlotService slotService,
        RegistrySeatService seatService,
        RegistryBedService bedService,
        RegistrySpawnService spawnService,
        RegistryResidentService residentService,
        RegistryDiagnosticsService diagnosticsService,
        RegistryDeletionService deletionService,
        RegistryFlushService flushService,
        RegistryWorldClockService worldClockService)
    {
        Log = log;
        ZoneService = zoneService;
        WaypointService = waypointService;
        SlotService = slotService;
        SeatService = seatService;
        BedService = bedService;
        SpawnService = spawnService;
        ResidentService = residentService;
        DiagnosticsService = diagnosticsService;
        DeletionService = deletionService;
        FlushService = flushService;
        WorldClockService = worldClockService;
    }
}
