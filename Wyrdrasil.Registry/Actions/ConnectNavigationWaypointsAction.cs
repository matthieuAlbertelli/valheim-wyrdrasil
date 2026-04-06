using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Actions;

public sealed class ConnectNavigationWaypointsAction : IRegistryAction
{
    public RegistryActionType ActionType => RegistryActionType.ConnectNavigationWaypoints;

    public void Execute(RegistryContext context)
    {
        context.WaypointService.ConnectNavigationWaypoints();
    }
}
