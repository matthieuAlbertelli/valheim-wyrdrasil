using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Actions;

public sealed class DeleteConstructionInTargetZoneAction : IRegistryAction
{
    public RegistryActionType ActionType => RegistryActionType.DeleteConstructionInTargetZone;

    public void Execute(RegistryContext context)
    {
        context.DeletionService.PurgeAllConstructionInTargetZone();
    }
}
