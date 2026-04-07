using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Actions;

public sealed class ForceAssignResidentAction : IRegistryAction
{
    public RegistryActionType ActionType => RegistryActionType.ForceAssignResident;

    public void Execute(RegistryContext context)
    {
        context.ResidentService.ForceAssignAtCrosshair();
    }
}
