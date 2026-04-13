using Wyrdrasil.Construction.Models;

namespace Wyrdrasil.Construction.Runtime;

public interface IConstructionRuntimeApi
{
    bool TryClaimWorkItem(int residentId, out ConstructionWorkTarget target);
    void ReleaseWorkItem(int residentId);
    bool TryContributeWork(int residentId, int projectId, int pieceId, float workAmount);
    bool IsProjectActive(int projectId);
    bool TryGetProject(int projectId, out ConstructionProjectData project);
}
