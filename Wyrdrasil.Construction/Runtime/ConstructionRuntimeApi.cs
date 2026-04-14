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
        _constructionProjectService.TryGetAssignedWorkPost(residentId, out var workPost);
        _constructionProjectService.TrySetResidentWorkActive(residentId, workPost.Id, false);
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

    public bool TryAssignResidentToProject(int residentId, int projectId, out ConstructionWorkPostData workPost, out string failureReason)
    {
        return _constructionProjectService.TryAssignResidentToProject(residentId, projectId, out workPost, out failureReason);
    }

    public bool TryClearResidentAssignment(int residentId, out int projectId, out int workPostId)
    {
        return _constructionProjectService.TryClearResidentAssignment(residentId, out projectId, out workPostId);
    }

    public bool TryRestoreResidentAssignment(int workPostId, int residentId)
    {
        return _constructionProjectService.TryRestoreResidentAssignment(workPostId, residentId);
    }

    public bool TryGetWorkPost(int workPostId, out ConstructionWorkPostData workPost)
    {
        return _constructionProjectService.TryGetWorkPost(workPostId, out workPost);
    }

    public bool TryGetAssignedWorkPost(int residentId, out ConstructionWorkPostData workPost)
    {
        return _constructionProjectService.TryGetAssignedWorkPost(residentId, out workPost);
    }

    public bool TrySetResidentWorkActive(int residentId, int workPostId, bool isActive)
    {
        return _constructionProjectService.TrySetResidentWorkActive(residentId, workPostId, isActive);
    }

    public int GetActiveWorkerCount(int projectId)
    {
        return _constructionProjectService.GetActiveWorkerCount(projectId);
    }

    public bool IsProjectActive(int projectId) => _constructionProjectService.IsProjectActive(projectId);

    public bool TryGetProject(int projectId, out ConstructionProjectData project) => _constructionProjectService.TryGetProject(projectId, out project);
}
