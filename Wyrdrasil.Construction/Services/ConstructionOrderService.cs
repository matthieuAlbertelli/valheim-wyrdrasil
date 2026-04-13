using System.Collections.Generic;
using System.Linq;
using Wyrdrasil.Construction.Models;

namespace Wyrdrasil.Construction.Services;

public sealed class ConstructionOrderService
{
    public IReadOnlyList<BlueprintPieceData> AssignBuildOrder(IEnumerable<BlueprintPieceData> pieces)
    {
        var ordered = pieces
            .OrderBy(piece => piece.LocalPosition.y)
            .ThenBy(piece => piece.LocalPosition.x)
            .ThenBy(piece => piece.LocalPosition.z)
            .ThenBy(piece => piece.PrefabName)
            .ToList();

        for (var index = 0; index < ordered.Count; index++)
        {
            ordered[index].BuildOrder = index;
        }

        return ordered;
    }
}
