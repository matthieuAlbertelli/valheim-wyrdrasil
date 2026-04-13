using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Actions;

public sealed class ClearTargetResidentConstructionAssignmentAction : IRegistryAction
{
    public RegistryActionType ActionType => RegistryActionType.ClearTargetResidentConstructionAssignment;

    public void Execute(RegistryContext context)
    {
        if (!context.ResidentService.TryGetTargetedRegisteredResident(out var resident))
        {
            context.Log.LogWarning("Aim at a registered resident to clear their construction assignment.");
            return;
        }

        if (!context.ConstructionTestingApi.TryClearResidentProjectAssignment(resident.Id, out var projectId, out var workPostId, out var failureReason))
        {
            context.Log.LogWarning($"Failed to clear construction assignment for resident #{resident.Id}: {failureReason}");
            return;
        }

        context.Log.LogInfo($"Cleared construction assignment for resident #{resident.Id} ('{resident.DisplayName}') from construction project {projectId}, work post #{workPostId}.");
    }
}
