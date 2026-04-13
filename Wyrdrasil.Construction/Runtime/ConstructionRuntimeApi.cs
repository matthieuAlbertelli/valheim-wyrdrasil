using Wyrdrasil.Construction.Diagnostics;
using Wyrdrasil.Construction.Models;
using Wyrdrasil.Construction.Services;

namespace Wyrdrasil.Construction.Runtime;

public sealed class ConstructionRuntimeApi : IConstructionRuntimeApi
{
    private readonly ConstructionProjectService _constructionProjectService;
    private readonly ConstructionDebugLogService _debugLogService;

    public ConstructionRuntimeApi(ConstructionProjectService constructionProjectService, ConstructionDebugLogService debugLogService)
    {
        _constructionProjectService = constructionProjectService;
        _debugLogService = debugLogService;
    }

    public bool TryClaimWorkItem(int residentId, out ConstructionWorkTarget target)
    {
        target = new ConstructionWorkTarget();
        _debugLogService.Verbose("Runtime", $"Resident {residentId} requested a construction work item, but runtime claiming is not implemented yet.");
        return false;
    }

    public void ReleaseWorkItem(int residentId)
    {
        _debugLogService.Verbose("Runtime", $"Resident {residentId} released a construction work item.");
    }

    public bool TryContributeWork(int residentId, int projectId, int pieceId, float workAmount)
    {
        var success = _constructionProjectService.TryAddAccumulatedPieceWork(projectId, workAmount, out _);
        if (success)
        {
            _debugLogService.Verbose(
                "Runtime",
                $"Resident {residentId} contributed {workAmount:0.##} aggregate work to construction project {projectId}.");
        }

        return success;
    }

    public bool IsProjectActive(int projectId) => _constructionProjectService.IsProjectActive(projectId);

    public bool TryGetProject(int projectId, out ConstructionProjectData project) => _constructionProjectService.TryGetProject(projectId, out project);
}
