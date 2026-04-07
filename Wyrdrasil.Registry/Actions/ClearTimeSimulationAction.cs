using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Actions;

public sealed class ClearTimeSimulationAction : IRegistryAction
{
    public RegistryActionType ActionType => RegistryActionType.ClearTimeSimulation;

    public void Execute(RegistryContext context)
    {
        context.WorldClockService.ClearSimulation();
    }
}
