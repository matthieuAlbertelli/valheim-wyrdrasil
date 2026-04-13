using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Actions;

public sealed class AssignTargetResidentToLatestConstructionProjectAction : IRegistryAction
{
    public RegistryActionType ActionType => RegistryActionType.AssignTargetResidentToLatestConstructionProject;

    public void Execute(RegistryContext context)
    {
        if (!context.ConstructionDebugSessionService.TryGetLatestProjectId(out var projectId))
        {
            context.Log.LogWarning("No latest construction project is available for resident assignment.");
            return;
        }

        if (!context.ResidentService.TryGetTargetedRegisteredResident(out var resident))
        {
            context.Log.LogWarning("Aim at a registered resident to assign them to the latest construction project.");
            return;
        }

        if (!context.ConstructionTestingApi.TryAssignResidentToProject(resident.Id, projectId, out var workPostId, out var failureReason))
        {
            context.Log.LogWarning($"Failed to assign resident #{resident.Id} to construction project {projectId}: {failureReason}");
            return;
        }

        context.Log.LogInfo($"Assigned resident #{resident.Id} ('{resident.DisplayName}') to construction project {projectId}, work post #{workPostId}.");
    }
}
