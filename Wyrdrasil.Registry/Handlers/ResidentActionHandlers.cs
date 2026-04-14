using Wyrdrasil.Registry.Services;

namespace Wyrdrasil.Registry.Handlers;

public sealed class RegisterNpcHandler : IRegistryActionHandler
{
    private readonly RegistryResidentService _residentService;

    public RegisterNpcHandler(RegistryResidentService residentService)
    {
        _residentService = residentService;
    }

    public void Execute()
    {
        _residentService.RegisterNpcAtCrosshair();
    }
}

public sealed class AssignInnkeeperRoleHandler : IRegistryActionHandler
{
    private readonly RegistryResidentService _residentService;

    public AssignInnkeeperRoleHandler(RegistryResidentService residentService)
    {
        _residentService = residentService;
    }

    public void Execute()
    {
        _residentService.AssignInnkeeperRoleAtCrosshair();
    }
}

public sealed class AssignSeatHandler : IRegistryActionHandler
{
    private readonly RegistryResidentService _residentService;

    public AssignSeatHandler(RegistryResidentService residentService)
    {
        _residentService = residentService;
    }

    public void Execute()
    {
        _residentService.AssignSeatAtCrosshair();
    }
}

public sealed class AssignBedHandler : IRegistryActionHandler
{
    private readonly RegistryResidentService _residentService;

    public AssignBedHandler(RegistryResidentService residentService)
    {
        _residentService = residentService;
    }

    public void Execute()
    {
        _residentService.AssignBedAtCrosshair();
    }
}

public sealed class ClearTargetInnkeeperSlotAssignmentHandler : IRegistryActionHandler
{
    private readonly RegistryResidentService _residentService;

    public ClearTargetInnkeeperSlotAssignmentHandler(RegistryResidentService residentService)
    {
        _residentService = residentService;
    }

    public void Execute()
    {
        _residentService.ClearTargetInnkeeperSlotAssignmentAtCrosshair();
    }
}

public sealed class ClearTargetSeatAssignmentHandler : IRegistryActionHandler
{
    private readonly RegistryResidentService _residentService;

    public ClearTargetSeatAssignmentHandler(RegistryResidentService residentService)
    {
        _residentService = residentService;
    }

    public void Execute()
    {
        _residentService.ClearTargetSeatAssignmentAtCrosshair();
    }
}

public sealed class ClearTargetBedAssignmentHandler : IRegistryActionHandler
{
    private readonly RegistryResidentService _residentService;

    public ClearTargetBedAssignmentHandler(RegistryResidentService residentService)
    {
        _residentService = residentService;
    }

    public void Execute()
    {
        _residentService.ClearTargetBedAssignmentAtCrosshair();
    }
}

public sealed class ForceAssignResidentHandler : IRegistryActionHandler
{
    private readonly RegistryResidentService _residentService;

    public ForceAssignResidentHandler(RegistryResidentService residentService)
    {
        _residentService = residentService;
    }

    public void Execute()
    {
        _residentService.ForceAssignAtCrosshair();
    }
}

public sealed class DespawnTargetResidentHandler : IRegistryActionHandler
{
    private readonly RegistryResidentService _residentService;

    public DespawnTargetResidentHandler(RegistryResidentService residentService)
    {
        _residentService = residentService;
    }

    public void Execute()
    {
        _residentService.DespawnTargetResidentAtCrosshair();
    }
}

public sealed class RespawnAssignedResidentHandler : IRegistryActionHandler
{
    private readonly RegistryResidentService _residentService;

    public RespawnAssignedResidentHandler(RegistryResidentService residentService)
    {
        _residentService = residentService;
    }

    public void Execute()
    {
        _residentService.RespawnAssignedResidentAtCrosshair();
    }
}

public sealed class ProbeAssignedCraftStationOccupationHandler : IRegistryActionHandler
{
    private readonly RegistryResidentService _residentService;

    public ProbeAssignedCraftStationOccupationHandler(RegistryResidentService residentService)
    {
        _residentService = residentService;
    }

    public void Execute()
    {
        _residentService.ProbeAssignedCraftStationOccupationAtCrosshair();
    }
}
