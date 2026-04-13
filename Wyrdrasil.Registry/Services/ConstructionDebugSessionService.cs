namespace Wyrdrasil.Registry.Services;

public sealed class ConstructionDebugSessionService
{
    public int LatestProjectId { get; private set; }

    public void SetLatestProjectId(int projectId)
    {
        LatestProjectId = projectId > 0 ? projectId : 0;
    }

    public bool TryGetLatestProjectId(out int projectId)
    {
        projectId = LatestProjectId;
        return projectId > 0;
    }

    public void Clear()
    {
        LatestProjectId = 0;
    }
}
