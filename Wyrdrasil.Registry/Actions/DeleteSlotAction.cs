using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Actions;

public sealed class DeleteSlotAction : IRegistryAction
{
    public RegistryActionType ActionType => RegistryActionType.DeleteSlot;
    public void Execute(RegistryContext context) => context.DeletionService.DeleteSlotAtCrosshair();
}
