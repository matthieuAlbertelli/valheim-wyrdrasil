using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Actions;

public sealed class ClearTargetResidentConstructionAssignmentAction : IRegistryAction
{
    public RegistryActionType ActionType => RegistryActionType.ClearTargetResidentConstructionAssignment;

    public void Execute(RegistryContext context)
    {
        if (!context.ResidentService.TryClearTargetedResidentConstructionAssignment(out var resident, out var projectId, out var workPostId, out var failureReason))
        {
            context.Log.LogWarning(failureReason);
            return;
        }

        context.Log.LogInfo($"Cleared construction assignment for resident #{resident.Id} ('{resident.DisplayName}') from construction project {projectId}, workbench binding #{workPostId}.");
    }
}
