using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Actions;

public sealed class ClearTargetInnkeeperSlotAssignmentAction : IRegistryAction
{
    public RegistryActionType ActionType => RegistryActionType.ClearTargetInnkeeperSlotAssignment;

    public void Execute(RegistryContext context)
    {
        context.ResidentService.ClearTargetInnkeeperSlotAssignmentAtCrosshair();
    }
}
