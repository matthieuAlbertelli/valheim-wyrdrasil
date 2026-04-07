using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Actions;

public sealed class SimulateNightAction : IRegistryAction
{
    public RegistryActionType ActionType => RegistryActionType.SimulateNight;

    public void Execute(RegistryContext context)
    {
        context.WorldClockService.SimulateNight();
    }
}
