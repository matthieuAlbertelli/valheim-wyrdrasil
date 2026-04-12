using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Actions;

public sealed class DeleteDesignatedCraftStationAction : IRegistryAction
{
    public RegistryActionType ActionType => RegistryActionType.DeleteDesignatedCraftStation;

    public void Execute(RegistryContext context)
    {
        context.DeletionService.DeleteDesignatedCraftStationAtCrosshair();
    }
}
