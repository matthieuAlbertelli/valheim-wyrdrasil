using BepInEx.Logging;
using Wyrdrasil.Core.Services;
using Wyrdrasil.Settlements.Services;

namespace Wyrdrasil.Registry.Bootstrap;

public sealed class RegistrySettlementsBootstrap
{
    public RegistrySettlementsBootstrap(
        BuildingService buildingService,
        ZoneDefinitionCatalog zoneDefinitionCatalog,
        ZonePlacementPolicyService zonePlacementPolicyService,
        FunctionalZoneService zoneService,
        NavigationWaypointService waypointService,
        ZoneSlotService slotService,
        SeatService seatService,
        BedService bedService,
        CraftStationService craftStationService,
        FunctionalZoneRuntimeService zoneRuntimeService)
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

    public static RegistrySettlementsBootstrap Create(ManualLogSource log, RegistryModeService modeService)
    {
        var buildingService = new BuildingService(log);
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

        return new RegistrySettlementsBootstrap(
            buildingService,
            zoneDefinitionCatalog,
            zonePlacementPolicyService,
            zoneService,
            waypointService,
            slotService,
            seatService,
            bedService,
            craftStationService,
            zoneRuntimeService);
    }
}
