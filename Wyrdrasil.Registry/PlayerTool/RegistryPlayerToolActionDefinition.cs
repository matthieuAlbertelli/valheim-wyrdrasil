namespace Wyrdrasil.Registry.PlayerTool;

public sealed class RegistryPlayerToolActionDefinition
{
    public RegistryPlayerToolActionDefinition(
        string piecePrefabName,
        string displayName,
        string description,
        int categoryIndex,
        string blueprintId = "")
    {
        PiecePrefabName = piecePrefabName;
        DisplayName = displayName;
        Description = description;
        CategoryIndex = categoryIndex;
        BlueprintId = blueprintId ?? string.Empty;
    }

    public string PiecePrefabName { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public int CategoryIndex { get; }
    public string BlueprintId { get; }
    public bool IsBlueprintPlan => !string.IsNullOrWhiteSpace(BlueprintId);
}
