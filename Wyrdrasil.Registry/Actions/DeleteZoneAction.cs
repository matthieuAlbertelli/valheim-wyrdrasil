using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Actions;

public sealed class DeleteZoneAction : IRegistryAction
{
    public RegistryActionType ActionType => RegistryActionType.DeleteZone;
    public void Execute(RegistryContext context) => context.DeletionService.DeleteZoneAtCrosshair();
}
