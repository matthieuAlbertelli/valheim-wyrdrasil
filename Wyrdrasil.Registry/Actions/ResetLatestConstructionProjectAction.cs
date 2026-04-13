using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Actions;

public sealed class ResetLatestConstructionProjectAction : IRegistryAction
{
    public RegistryActionType ActionType => RegistryActionType.ResetLatestConstructionProject;

    public void Execute(RegistryContext context)
    {
        if (!context.ConstructionDebugSessionService.TryGetLatestProjectId(out var projectId))
        {
            context.Log.LogWarning("No latest construction project is available to reset.");
            return;
        }

        if (!context.ConstructionTestingApi.TryResetProject(projectId, out var failureReason))
        {
            context.Log.LogWarning($"Failed to reset construction project {projectId}: {failureReason}");
            return;
        }

        context.Log.LogInfo($"Reset construction project {projectId}.");
    }
}