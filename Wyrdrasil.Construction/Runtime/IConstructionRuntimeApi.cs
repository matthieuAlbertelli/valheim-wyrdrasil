using Wyrdrasil.Construction.Models;

namespace Wyrdrasil.Construction.Runtime;

public interface IConstructionRuntimeApi
{
    bool TryClaimWorkItem(int residentId, out ConstructionWorkTarget target);
    void ReleaseWorkItem(int residentId);
    bool TryContributeWork(int residentId, int projectId, int pieceId, float workAmount);
    bool TryAssignResidentToProject(int residentId, int projectId, out ConstructionWorkPostData workPost, out string failureReason);
    bool TryClearResidentAssignment(int residentId, out int projectId, out int workPostId);
    bool TryRestoreResidentAssignment(int workPostId, int residentId);
    bool TryGetWorkPost(int workPostId, out ConstructionWorkPostData workPost);
    bool TryGetAssignedWorkPost(int residentId, out ConstructionWorkPostData workPost);
    bool TrySetResidentWorkActive(int residentId, int workPostId, bool isActive);
    int GetActiveWorkerCount(int projectId);
    bool IsProjectActive(int projectId);
    bool TryGetProject(int projectId, out ConstructionProjectData project);
}
