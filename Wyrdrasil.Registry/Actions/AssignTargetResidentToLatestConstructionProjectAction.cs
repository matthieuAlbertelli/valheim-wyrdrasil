using Wyrdrasil.Core.Tool;
using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Actions;

public sealed class AssignTargetResidentToLatestConstructionProjectAction : IRegistryAction
{
    public RegistryActionType ActionType => RegistryActionType.AssignTargetResidentToLatestConstructionProject;

    public void Execute(RegistryContext context)
    {
        if (!context.ConstructionDebugSessionService.TryGetPendingResidentProjectId(out var projectId))
        {
            if (!context.ConstructionProjectMarkerService.TryGetTargetedProjectId(out projectId))
            {
                context.Log.LogWarning("Aim at a construction marker to select a chantier for resident assignment.");
                return;
            }

            context.ConstructionDebugSessionService.BeginPendingResidentSelection(projectId);
            context.Log.LogInfo($"Selected construction project #{projectId} for resident assignment. Now aim at a registered resident and click again.");
            return;
        }

        if (context.ConstructionProjectMarkerService.TryGetTargetedProjectId(out var reselectedProjectId))
        {
            context.ConstructionDebugSessionService.BeginPendingResidentSelection(reselectedProjectId);
            context.Log.LogInfo($"Switched selected construction project to #{reselectedProjectId} for resident assignment. Now aim at a registered resident and click again.");
            return;
        }

        if (!context.ResidentService.TryAssignTargetedResidentToConstructionProject(projectId, out var resident, out var workPostId, out var failureReason))
        {
            context.Log.LogWarning($"Failed to assign targeted resident to construction project #{projectId}: {failureReason}");
            return;
        }

        context.ConstructionDebugSessionService.ClearPendingResidentSelection();
        context.ConstructionDebugSessionService.SetLatestProjectId(projectId);
        context.Log.LogInfo($"Assigned resident #{resident.Id} ('{resident.DisplayName}') to construction project #{projectId}, logical slot #{workPostId}.");
    }
}
