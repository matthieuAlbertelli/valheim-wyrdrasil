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

        if (!context.ResidentService.TryAssignTargetedResidentToConstructionProject(projectId, out var resident, out var workPostId, out var failureReason))
        {
            context.Log.LogWarning(failureReason);
            return;
        }

        context.Log.LogInfo($"Assigned resident #{resident.Id} ('{resident.DisplayName}') to construction project {projectId}, workbench binding #{workPostId}.");
    }
}
