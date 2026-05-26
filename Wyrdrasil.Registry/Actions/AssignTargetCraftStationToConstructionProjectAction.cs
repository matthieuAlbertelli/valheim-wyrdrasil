using Wyrdrasil.Core.Tool;
using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Actions;

public sealed class AssignTargetCraftStationToConstructionProjectAction : IRegistryAction
{
    public RegistryActionType ActionType => RegistryActionType.AssignTargetCraftStationToConstructionProject;

    public void Execute(RegistryContext context)
    {
        if (!context.ConstructionDebugSessionService.TryGetPendingCraftStationProjectId(out var projectId))
        {
            if (!context.ConstructionProjectMarkerService.TryGetTargetedProjectId(out projectId))
            {
                context.Log.LogWarning("Aim at a construction marker to select a chantier for workbench association.");
                return;
            }

            context.ConstructionDebugSessionService.BeginPendingCraftStationSelection(projectId);
            context.Log.LogInfo($"Selected construction project #{projectId} for workbench association. Now aim at a designated workbench and click again.");
            return;
        }

        if (context.ConstructionProjectMarkerService.TryGetTargetedProjectId(out var reselectedProjectId))
        {
            context.ConstructionDebugSessionService.BeginPendingCraftStationSelection(reselectedProjectId);
            context.Log.LogInfo($"Switched selected construction project to #{reselectedProjectId} for workbench association. Now aim at a designated workbench and click again.");
            return;
        }

        if (!context.CraftStationService.TryGetOrDesignateCraftStationAtCrosshair(out var craftStation, out var failureReason))
        {
            context.Log.LogWarning(failureReason);
            return;
        }

        if (craftStation.AssignedRegisteredNpcId.HasValue)
        {
            context.Log.LogWarning($"Workbench #{craftStation.Id} is already assigned to resident #{craftStation.AssignedRegisteredNpcId.Value} for regular work. Clear that assignment before reserving it for a chantier.");
            return;
        }

        if (!context.ConstructionRuntimeApi.TryAssignCraftStationToProject(craftStation.Id, projectId, out var workPost, out var assignmentFailureReason))
        {
            context.Log.LogWarning($"Failed to associate workbench #{craftStation.Id} with construction project #{projectId}: {assignmentFailureReason}");
            return;
        }

        context.ConstructionDebugSessionService.SetLatestProjectId(projectId);
        context.Log.LogInfo($"Associated designated workbench #{craftStation.Id} with construction project #{projectId}, work post #{workPost.Id}. Project remains selected for more associations.");
    }
}
