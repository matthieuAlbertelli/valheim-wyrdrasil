using Wyrdrasil.Registry.Services;
using Wyrdrasil.Settlements.Authoring;

namespace Wyrdrasil.Registry.Handlers;

public sealed class CreateTavernZoneHandler : IRegistryActionHandler
{
    private readonly ISettlementsAuthoringApi _settlementsAuthoringApi;

    public CreateTavernZoneHandler(ISettlementsAuthoringApi settlementsAuthoringApi)
    {
        _settlementsAuthoringApi = settlementsAuthoringApi;
    }

    public void Execute()
    {
        _settlementsAuthoringApi.CreateTavernZone();
    }
}

public sealed class CreateBedroomZoneHandler : IRegistryActionHandler
{
    private readonly ISettlementsAuthoringApi _settlementsAuthoringApi;

    public CreateBedroomZoneHandler(ISettlementsAuthoringApi settlementsAuthoringApi)
    {
        _settlementsAuthoringApi = settlementsAuthoringApi;
    }

    public void Execute()
    {
        _settlementsAuthoringApi.CreateBedroomZone();
    }
}

public sealed class CreateNavigationWaypointHandler : IRegistryActionHandler
{
    private readonly ISettlementsAuthoringApi _settlementsAuthoringApi;

    public CreateNavigationWaypointHandler(ISettlementsAuthoringApi settlementsAuthoringApi)
    {
        _settlementsAuthoringApi = settlementsAuthoringApi;
    }

    public void Execute()
    {
        _settlementsAuthoringApi.CreateNavigationWaypoint();
    }
}

public sealed class ConnectNavigationWaypointsHandler : IRegistryActionHandler
{
    private readonly ISettlementsAuthoringApi _settlementsAuthoringApi;

    public ConnectNavigationWaypointsHandler(ISettlementsAuthoringApi settlementsAuthoringApi)
    {
        _settlementsAuthoringApi = settlementsAuthoringApi;
    }

    public void Execute()
    {
        _settlementsAuthoringApi.ConnectNavigationWaypoints();
    }
}

public sealed class CreateInnkeeperSlotHandler : IRegistryActionHandler
{
    private readonly ISettlementsAuthoringApi _settlementsAuthoringApi;

    public CreateInnkeeperSlotHandler(ISettlementsAuthoringApi settlementsAuthoringApi)
    {
        _settlementsAuthoringApi = settlementsAuthoringApi;
    }

    public void Execute()
    {
        _settlementsAuthoringApi.CreateInnkeeperSlot();
    }
}

public sealed class DesignateSeatFurnitureHandler : IRegistryActionHandler
{
    private readonly ISettlementsAuthoringApi _settlementsAuthoringApi;

    public DesignateSeatFurnitureHandler(ISettlementsAuthoringApi settlementsAuthoringApi)
    {
        _settlementsAuthoringApi = settlementsAuthoringApi;
    }

    public void Execute()
    {
        _settlementsAuthoringApi.DesignateSeatAtCrosshair();
    }
}

public sealed class DesignateBedFurnitureHandler : IRegistryActionHandler
{
    private readonly ISettlementsAuthoringApi _settlementsAuthoringApi;

    public DesignateBedFurnitureHandler(ISettlementsAuthoringApi settlementsAuthoringApi)
    {
        _settlementsAuthoringApi = settlementsAuthoringApi;
    }

    public void Execute()
    {
        _settlementsAuthoringApi.DesignateBedAtCrosshair();
    }
}

public sealed class DesignateCraftStationFurnitureHandler : IRegistryActionHandler
{
    private readonly ISettlementsAuthoringApi _settlementsAuthoringApi;

    public DesignateCraftStationFurnitureHandler(ISettlementsAuthoringApi settlementsAuthoringApi)
    {
        _settlementsAuthoringApi = settlementsAuthoringApi;
    }

    public void Execute()
    {
        _settlementsAuthoringApi.DesignateCraftStationAtCrosshair();
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
