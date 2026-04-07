using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Actions;

public sealed class DespawnTargetResidentAction : IRegistryAction
{
    public RegistryActionType ActionType => RegistryActionType.DespawnTargetResident;

    public void Execute(RegistryContext context)
    {
        context.ResidentService.DespawnTargetResidentAtCrosshair();
    }
}
