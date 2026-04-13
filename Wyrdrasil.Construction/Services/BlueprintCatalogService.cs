using System.Collections.Generic;
using System.Linq;
using Wyrdrasil.Construction.Models;

namespace Wyrdrasil.Construction.Services;

public sealed class BlueprintCatalogService
{
    private readonly Dictionary<string, StructureBlueprintData> _blueprintsById = new();

    public IReadOnlyList<StructureBlueprintData> Blueprints => _blueprintsById.Values.ToList();

    public void Clear() => _blueprintsById.Clear();

    public void LoadBlueprints(IEnumerable<StructureBlueprintData> blueprints)
    {
        _blueprintsById.Clear();
        foreach (var blueprint in blueprints)
        {
            _blueprintsById[blueprint.Id] = blueprint;
        }
    }

    public void UpsertBlueprint(StructureBlueprintData blueprint) => _blueprintsById[blueprint.Id] = blueprint;

    public bool TryGetBlueprint(string blueprintId, out StructureBlueprintData blueprint) => _blueprintsById.TryGetValue(blueprintId, out blueprint!);
}
