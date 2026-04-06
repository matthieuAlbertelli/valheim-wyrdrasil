using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Actions;

public sealed class CreateTavernWaypointAction : IRegistryAction
{
    public RegistryActionType ActionType => RegistryActionType.CreateNavigationWaypoint;

    public void Execute(RegistryContext context)
    {
        context.WaypointService.CreateNavigationWaypoint();
    }
}
