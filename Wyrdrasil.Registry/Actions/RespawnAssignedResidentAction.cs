using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Actions;

public sealed class RespawnAssignedResidentAction : IRegistryAction
{
    public RegistryActionType ActionType => RegistryActionType.RespawnAssignedResident;

    public void Execute(RegistryContext context)
    {
        context.ResidentService.RespawnAssignedResidentAtCrosshair();
    }
}
