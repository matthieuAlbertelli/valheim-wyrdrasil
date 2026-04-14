using Wyrdrasil.Core.Tool;
using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Actions;

public sealed class DumpLatestConstructionProjectStateAction : IRegistryAction
{
    public RegistryActionType ActionType => RegistryActionType.DumpLatestConstructionProjectState;

    public void Execute(RegistryContext context)
    {
        if (!context.ConstructionDebugSessionService.TryGetLatestProjectId(out var projectId))
        {
            context.Log.LogWarning("No latest construction project is available to dump.");
            return;
        }

        if (!context.ConstructionTestingApi.TryDumpProjectState(projectId, out var dump, out var failureReason))
        {
            context.Log.LogWarning($"Failed to dump construction project {projectId}: {failureReason}");
            return;
        }

        context.Log.LogInfo($"Construction project {projectId} state dump:");
        foreach (var line in dump.Split('\n'))
        {
            context.Log.LogInfo(line.TrimEnd('\r'));
        }
    }
}