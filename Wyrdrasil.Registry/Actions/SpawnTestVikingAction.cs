using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Actions;

public sealed class SpawnTestVikingAction : IRegistryAction
{
    public RegistryActionType ActionType => RegistryActionType.SpawnTestViking;

    public void Execute(RegistryContext context)
    {
        context.SpawnService.SpawnTestViking();
    }
}
