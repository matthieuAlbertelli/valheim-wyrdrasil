using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Actions;

public sealed class AssignInnkeeperRoleAction : IRegistryAction
{
    public RegistryActionType ActionType => RegistryActionType.AssignInnkeeperRole;

    public void Execute(RegistryContext context)
    {
        context.ResidentService.AssignInnkeeperRoleAtCrosshair();
    }
}
