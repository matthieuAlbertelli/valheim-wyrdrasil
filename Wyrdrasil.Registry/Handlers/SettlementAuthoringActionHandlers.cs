using Wyrdrasil.Registry.Services;
using Wyrdrasil.Settlements.Services;

namespace Wyrdrasil.Registry.Handlers;

public sealed class CreateTavernZoneHandler : IRegistryActionHandler
{
    private readonly FunctionalZoneService _zoneService;

    public CreateTavernZoneHandler(FunctionalZoneService zoneService)
    {
        _zoneService = zoneService;
    }

    public void Execute()
    {
        _zoneService.CreateTavernZone();
    }
}

public sealed class CreateBedroomZoneHandler : IRegistryActionHandler
{
    private readonly FunctionalZoneService _zoneService;

    public CreateBedroomZoneHandler(FunctionalZoneService zoneService)
    {
        _zoneService = zoneService;
    }

    public void Execute()
    {
        _zoneService.CreateBedroomZone();
    }
}

public sealed class CreateNavigationWaypointHandler : IRegistryActionHandler
{
    private readonly NavigationWaypointService _waypointService;

    public CreateNavigationWaypointHandler(NavigationWaypointService waypointService)
    {
        _waypointService = waypointService;
    }

    public void Execute()
    {
        _waypointService.CreateNavigationWaypoint();
    }
}

public sealed class ConnectNavigationWaypointsHandler : IRegistryActionHandler
{
    private readonly NavigationWaypointService _waypointService;

    public ConnectNavigationWaypointsHandler(NavigationWaypointService waypointService)
    {
        _waypointService = waypointService;
    }

    public void Execute()
    {
        _waypointService.ConnectNavigationWaypoints();
    }
}

public sealed class CreateInnkeeperSlotHandler : IRegistryActionHandler
{
    private readonly ZoneSlotService _slotService;

    public CreateInnkeeperSlotHandler(ZoneSlotService slotService)
    {
        _slotService = slotService;
    }

    public void Execute()
    {
        _slotService.CreateInnkeeperSlot();
    }
}

public sealed class DesignateSeatFurnitureHandler : IRegistryActionHandler
{
    private readonly SeatService _seatService;

    public DesignateSeatFurnitureHandler(SeatService seatService)
    {
        _seatService = seatService;
    }

    public void Execute()
    {
        _seatService.DesignateSeatAtCrosshair();
    }
}

public sealed class DesignateBedFurnitureHandler : IRegistryActionHandler
{
    private readonly BedService _bedService;

    public DesignateBedFurnitureHandler(BedService bedService)
    {
        _bedService = bedService;
    }

    public void Execute()
    {
        _bedService.DesignateBedAtCrosshair();
    }
}

public sealed class DesignateCraftStationFurnitureHandler : IRegistryActionHandler
{
    private readonly CraftStationService _craftStationService;

    public DesignateCraftStationFurnitureHandler(CraftStationService craftStationService)
    {
        _craftStationService = craftStationService;
    }

    public void Execute()
    {
        _craftStationService.DesignateCraftStationAtCrosshair();
    }
}

public sealed class DeleteZoneHandler : IRegistryActionHandler
{
    private readonly RegistryDeletionService _deletionService;

    public DeleteZoneHandler(RegistryDeletionService deletionService)
    {
        _deletionService = deletionService;
    }

    public void Execute()
    {
        _deletionService.DeleteZoneAtCrosshair();
    }
}

public sealed class DeleteSlotHandler : IRegistryActionHandler
{
    private readonly RegistryDeletionService _deletionService;

    public DeleteSlotHandler(RegistryDeletionService deletionService)
    {
        _deletionService = deletionService;
    }

    public void Execute()
    {
        _deletionService.DeleteSlotAtCrosshair();
    }
}

public sealed class DeleteDesignatedSeatHandler : IRegistryActionHandler
{
    private readonly RegistryDeletionService _deletionService;

    public DeleteDesignatedSeatHandler(RegistryDeletionService deletionService)
    {
        _deletionService = deletionService;
    }

    public void Execute()
    {
        _deletionService.DeleteDesignatedSeatAtCrosshair();
    }
}

public sealed class DeleteDesignatedBedHandler : IRegistryActionHandler
{
    private readonly RegistryDeletionService _deletionService;

    public DeleteDesignatedBedHandler(RegistryDeletionService deletionService)
    {
        _deletionService = deletionService;
    }

    public void Execute()
    {
        _deletionService.DeleteDesignatedBedAtCrosshair();
    }
}

public sealed class DeleteDesignatedCraftStationHandler : IRegistryActionHandler
{
    private readonly RegistryDeletionService _deletionService;

    public DeleteDesignatedCraftStationHandler(RegistryDeletionService deletionService)
    {
        _deletionService = deletionService;
    }

    public void Execute()
    {
        _deletionService.DeleteDesignatedCraftStationAtCrosshair();
    }
}

public sealed class DeleteNavigationWaypointHandler : IRegistryActionHandler
{
    private readonly RegistryDeletionService _deletionService;

    public DeleteNavigationWaypointHandler(RegistryDeletionService deletionService)
    {
        _deletionService = deletionService;
    }

    public void Execute()
    {
        _deletionService.DeleteNavigationWaypointAtCrosshair();
    }
}
