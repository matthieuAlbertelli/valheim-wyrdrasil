using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Actions;

public sealed class DeleteNavigationWaypointAction : IRegistryAction
{
    public RegistryActionType ActionType => RegistryActionType.DeleteNavigationWaypoint;
    public void Execute(RegistryContext context) => context.DeletionService.DeleteNavigationWaypointAtCrosshair();
}
