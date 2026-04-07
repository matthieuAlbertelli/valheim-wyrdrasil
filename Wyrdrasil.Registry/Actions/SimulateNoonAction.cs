using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Actions;

public sealed class SimulateNoonAction : IRegistryAction
{
    public RegistryActionType ActionType => RegistryActionType.SimulateNoon;

    public void Execute(RegistryContext context)
    {
        context.WorldClockService.SimulateNoon();
    }
}
