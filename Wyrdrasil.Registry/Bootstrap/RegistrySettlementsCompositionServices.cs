using Wyrdrasil.Settlements.Services;

namespace Wyrdrasil.Registry.Bootstrap;

internal sealed class RegistrySettlementsCompositionServices
{
    public RegistrySettlementsCompositionServices(
        BuildingService buildingService,
        FunctionalZoneService zoneService,
        NavigationWaypointService waypointService,
        ZoneSlotService slotService,
        SeatService seatService,
        BedService bedService,
        CraftStationService craftStationService)
    {
        BuildingService = buildingService;
        ZoneService = zoneService;
        WaypointService = waypointService;
        SlotService = slotService;
        SeatService = seatService;
        BedService = bedService;
        CraftStationService = craftStationService;
    }

    public BuildingService BuildingService { get; }
    public FunctionalZoneService ZoneService { get; }
    public NavigationWaypointService WaypointService { get; }
    public ZoneSlotService SlotService { get; }
    public SeatService SeatService { get; }
    public BedService BedService { get; }
    public CraftStationService CraftStationService { get; }
}
