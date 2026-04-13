using BepInEx.Logging;
using Wyrdrasil.Construction.Authoring;
using Wyrdrasil.Registry.Services;
using Wyrdrasil.Construction.Testing;
using Wyrdrasil.Routines.Services;
using Wyrdrasil.Settlements.Services;
using Wyrdrasil.Souls.Services;

namespace Wyrdrasil.Registry.Tool;

public sealed class RegistryContext
{
    public ManualLogSource Log { get; }
    public FunctionalZoneService ZoneService { get; }
    public NavigationWaypointService WaypointService { get; }
    public ZoneSlotService SlotService { get; }
    public SeatService SeatService { get; }
    public BedService BedService { get; }
    public CraftStationService CraftStationService { get; }
    public NpcSpawnService SpawnService { get; }
    public RegistryResidentService ResidentService { get; }
    public TargetDiagnosticsService DiagnosticsService { get; }
    public CraftStationAnchorEditorService CraftStationAnchorEditorService { get; }
    public RegistryDeletionService DeletionService { get; }
    public RegistryFlushService FlushService { get; }
    public WorldClockService WorldClockService { get; }
    public IConstructionAuthoringApi ConstructionAuthoringApi { get; }
    public IConstructionTestingApi ConstructionTestingApi { get; }

    public RegistryContext(
        ManualLogSource log,
        FunctionalZoneService zoneService,
        NavigationWaypointService waypointService,
        ZoneSlotService slotService,
        SeatService seatService,
        BedService bedService,
        CraftStationService craftStationService,
        NpcSpawnService spawnService,
        RegistryResidentService residentService,
        TargetDiagnosticsService diagnosticsService,
        CraftStationAnchorEditorService craftStationAnchorEditorService,
        RegistryDeletionService deletionService,
        RegistryFlushService flushService,
        IConstructionAuthoringApi constructionAuthoringApi,
        IConstructionTestingApi constructionTestingApi,
        WorldClockService worldClockService)
    {
        Log = log;
        ZoneService = zoneService;
        WaypointService = waypointService;
        SlotService = slotService;
        SeatService = seatService;
        BedService = bedService;
        CraftStationService = craftStationService;
        SpawnService = spawnService;
        ResidentService = residentService;
        DiagnosticsService = diagnosticsService;
        CraftStationAnchorEditorService = craftStationAnchorEditorService;
        DeletionService = deletionService;
        FlushService = flushService;
        ConstructionAuthoringApi = constructionAuthoringApi;
        ConstructionTestingApi = constructionTestingApi;
        WorldClockService = worldClockService;
    }
}
