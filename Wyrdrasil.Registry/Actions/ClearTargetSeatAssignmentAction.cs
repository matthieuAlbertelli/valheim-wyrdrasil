using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Actions;

public sealed class ClearTargetSeatAssignmentAction : IRegistryAction
{
    public RegistryActionType ActionType => RegistryActionType.ClearTargetSeatAssignment;

    public void Execute(RegistryContext context)
    {
        context.ResidentService.ClearTargetSeatAssignmentAtCrosshair();
    }
}
