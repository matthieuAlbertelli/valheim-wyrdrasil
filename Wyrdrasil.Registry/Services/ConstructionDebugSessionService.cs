namespace Wyrdrasil.Registry.Services;

public sealed class ConstructionDebugSessionService
{
    public string LatestBlueprintId { get; private set; } = string.Empty;
    public int LatestProjectId { get; private set; }

    public void SetLatestBlueprintId(string blueprintId)
    {
        LatestBlueprintId = blueprintId ?? string.Empty;
    }

    public void SetLatestProjectId(int projectId)
    {
        LatestProjectId = projectId > 0 ? projectId : 0;
    }

    public void SetLatestBlueprintAndProject(string blueprintId, int projectId)
    {
        SetLatestBlueprintId(blueprintId);
        SetLatestProjectId(projectId);
    }

    public bool TryGetLatestBlueprintId(out string blueprintId)
    {
        blueprintId = LatestBlueprintId;
        return !string.IsNullOrWhiteSpace(blueprintId);
    }

    public bool TryGetLatestProjectId(out int projectId)
    {
        projectId = LatestProjectId;
        return projectId > 0;
    }

    public void Clear()
    {
        LatestBlueprintId = string.Empty;
        LatestProjectId = 0;
    }
}
