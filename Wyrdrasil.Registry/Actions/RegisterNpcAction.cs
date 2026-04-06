using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Actions;

public sealed class RegisterNpcAction : IRegistryAction
{
    public RegistryActionType ActionType => RegistryActionType.RegisterNpc;

    public void Execute(RegistryContext context)
    {
        context.ResidentService.RegisterNpcAtCrosshair();
    }
}
