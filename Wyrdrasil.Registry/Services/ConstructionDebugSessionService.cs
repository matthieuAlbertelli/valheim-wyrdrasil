namespace Wyrdrasil.Registry.Services;

public sealed class ConstructionDebugSessionService
{
    public string LatestBlueprintId { get; private set; } = string.Empty;
    public int LatestProjectId { get; private set; }
    public int PendingCraftStationProjectId { get; private set; }
    public int PendingResidentProjectId { get; private set; }

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

    public void BeginPendingCraftStationSelection(int projectId)
    {
        PendingCraftStationProjectId = projectId > 0 ? projectId : 0;
    }

    public bool TryGetPendingCraftStationProjectId(out int projectId)
    {
        projectId = PendingCraftStationProjectId;
        return projectId > 0;
    }

    public void ClearPendingCraftStationSelection()
    {
        PendingCraftStationProjectId = 0;
    }

    public void BeginPendingResidentSelection(int projectId)
    {
        PendingResidentProjectId = projectId > 0 ? projectId : 0;
    }

    public bool TryGetPendingResidentProjectId(out int projectId)
    {
        projectId = PendingResidentProjectId;
        return projectId > 0;
    }

    public void ClearPendingResidentSelection()
    {
        PendingResidentProjectId = 0;
    }

    public void Clear()
    {
        LatestBlueprintId = string.Empty;
        LatestProjectId = 0;
        PendingCraftStationProjectId = 0;
        PendingResidentProjectId = 0;
    }
}
