using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Actions;

public sealed class ForceCompleteLatestConstructionProjectAction : IRegistryAction
{
    public RegistryActionType ActionType => RegistryActionType.ForceCompleteLatestConstructionProject;

    public void Execute(RegistryContext context)
    {
        if (!context.ConstructionDebugSessionService.TryGetLatestProjectId(out var projectId))
        {
            context.Log.LogWarning("No latest construction project is available to force-complete.");
            return;
        }

        if (!context.ConstructionTestingApi.TryForceCompleteProject(projectId, out var builtPieceCount,
                out var failureReason))
        {
            context.Log.LogWarning($"Failed to force-complete construction project {projectId}: {failureReason}");
            return;
        }

        context.Log.LogInfo(
            $"Force-completed construction project {projectId}. Newly completed pieces: {builtPieceCount}.");
    }
}