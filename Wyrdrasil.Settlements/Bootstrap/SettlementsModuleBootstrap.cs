using BepInEx.Logging;
using Wyrdrasil.Core.Services;
using Wyrdrasil.Settlements.Authoring;
using Wyrdrasil.Settlements.Runtime;
using Wyrdrasil.Settlements.Services;

namespace Wyrdrasil.Settlements.Bootstrap;

public sealed class SettlementsModuleBootstrap
{
    public SettlementsModuleBootstrap(
        BuildingService buildingService,
        ZoneDefinitionCatalog zoneDefinitionCatalog,
        ZonePlacementPolicyService zonePlacementPolicyService,
        FunctionalZoneService zoneService,
        NavigationWaypointService waypointService,
        ZoneSlotService slotService,
        SeatService seatService,
        BedService bedService,
        CraftStationService craftStationService,
        FunctionalZoneRuntimeService zoneRuntimeService,
        SettlementsPersistenceParticipant persistenceParticipant,
        ISettlementsAuthoringApi authoringApi,
        ISettlementsRuntimeApi runtimeApi)
    {
        BuildingService = buildingService;
        ZoneDefinitionCatalog = zoneDefinitionCatalog;
        ZonePlacementPolicyService = zonePlacementPolicyService;
        ZoneService = zoneService;
        WaypointService = waypointService;
        SlotService = slotService;
        SeatService = seatService;
        BedService = bedService;
        CraftStationService = craftStationService;
        ZoneRuntimeService = zoneRuntimeService;
        PersistenceParticipant = persistenceParticipant;
        AuthoringApi = authoringApi;
        RuntimeApi = runtimeApi;
    }

    public BuildingService BuildingService { get; }
    public ZoneDefinitionCatalog ZoneDefinitionCatalog { get; }
    public ZonePlacementPolicyService ZonePlacementPolicyService { get; }
    public FunctionalZoneService ZoneService { get; }
    public NavigationWaypointService WaypointService { get; }
    public ZoneSlotService SlotService { get; }
    public SeatService SeatService { get; }
    public BedService BedService { get; }
    public CraftStationService CraftStationService { get; }
    public FunctionalZoneRuntimeService ZoneRuntimeService { get; }
    public SettlementsPersistenceParticipant PersistenceParticipant { get; }
    public ISettlementsAuthoringApi AuthoringApi { get; }
    public ISettlementsRuntimeApi RuntimeApi { get; }

    public static SettlementsModuleBootstrap Create(ManualLogSource log, RegistryModeService modeService)
    {
        var buildingService = new BuildingService(log, modeService);
        var zoneDefinitionCatalog = new ZoneDefinitionCatalog();
        var zonePlacementPolicyService = new ZonePlacementPolicyService(zoneDefinitionCatalog);
        var zoneService = new FunctionalZoneService(log, modeService, buildingService);
        var waypointService = new NavigationWaypointService(log, modeService, zoneService);
        var slotService = new ZoneSlotService(log, modeService, zoneService, zonePlacementPolicyService);
        var seatService = new SeatService(log, modeService, buildingService, zoneService, zonePlacementPolicyService);
        var bedService = new BedService(log, modeService, buildingService, zoneService, zonePlacementPolicyService);
        var craftStationService = new CraftStationService(log, modeService, buildingService, zoneService, zonePlacementPolicyService);
        var zoneRuntimeService = new FunctionalZoneRuntimeService(
            zoneDefinitionCatalog,
            zoneService,
            slotService,
            seatService,
            bedService,
            craftStationService);

        var persistenceParticipant = new SettlementsPersistenceParticipant(
            log,
            buildingService,
            zoneService,
            waypointService,
            slotService,
            seatService,
            bedService,
            craftStationService);

        var authoringApi = new SettlementsAuthoringApi(
            buildingService,
            zoneService,
            waypointService,
            slotService,
            seatService,
            bedService,
            craftStationService);

        var runtimeApi = new SettlementsRuntimeApi(
            buildingService,
            zoneService,
            waypointService,
            slotService,
            seatService,
            bedService,
            craftStationService,
            zoneRuntimeService);

        return new SettlementsModuleBootstrap(
            buildingService,
            zoneDefinitionCatalog,
            zonePlacementPolicyService,
            zoneService,
            waypointService,
            slotService,
            seatService,
            bedService,
            craftStationService,
            zoneRuntimeService,
            persistenceParticipant,
            authoringApi,
            runtimeApi);
    }
}
