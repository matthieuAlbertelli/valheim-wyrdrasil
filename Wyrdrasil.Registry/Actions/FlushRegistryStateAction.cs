using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Actions;

public sealed class FlushRegistryStateAction : IRegistryAction
{
    public RegistryActionType ActionType => RegistryActionType.FlushRegistryState;

    public void Execute(RegistryContext context)
    {
        context.FlushService.FlushAllRegistryState();
    }
}
