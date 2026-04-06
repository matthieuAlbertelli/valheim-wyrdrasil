using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Actions;

public sealed class DeleteDesignatedSeatAction : IRegistryAction
{
    public RegistryActionType ActionType => RegistryActionType.DeleteDesignatedSeat;

    public void Execute(RegistryContext context)
    {
        context.DeletionService.DeleteDesignatedSeatAtCrosshair();
    }
}
