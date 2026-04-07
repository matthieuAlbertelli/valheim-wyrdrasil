using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Actions;

public sealed class ClearTargetBedAssignmentAction : IRegistryAction
{
    public RegistryActionType ActionType => RegistryActionType.ClearTargetBedAssignment;

    public void Execute(RegistryContext context)
    {
        context.ResidentService.ClearTargetBedAssignmentAtCrosshair();
    }
}
