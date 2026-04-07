using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Actions;

public sealed class DeleteDesignatedBedAction : IRegistryAction
{
    public RegistryActionType ActionType => RegistryActionType.DeleteDesignatedBed;

    public void Execute(RegistryContext context)
    {
        context.DeletionService.DeleteDesignatedBedAtCrosshair();
    }
}
