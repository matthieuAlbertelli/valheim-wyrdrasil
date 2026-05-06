using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Wyrdrasil.Construction.Models;

namespace Wyrdrasil.Construction.Services;

public sealed class ConstructionOrderService
{
    private const float StructuralDependencyRadius = 3.25f;
    private const float LowerOrSameLayerTolerance = 0.35f;

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

        AssignApproximateStructuralDependencies(ordered);
        return ordered;
    }

    private static void AssignApproximateStructuralDependencies(IReadOnlyList<BlueprintPieceData> ordered)
    {
        if (ordered.Count == 0)
        {
            return;
        }

        var lowestY = ordered.Min(piece => piece.LocalPosition.y);
        foreach (var piece in ordered)
        {
            piece.DependencyPieceIds ??= new List<int>();
            piece.DependencyPieceIds.Clear();

            if (piece.LocalPosition.y <= lowestY + LowerOrSameLayerTolerance)
            {
                continue;
            }

            var dependencies = ordered
                .Where(candidate => candidate.PieceId != piece.PieceId)
                .Where(candidate => candidate.LocalPosition.y <= piece.LocalPosition.y + LowerOrSameLayerTolerance)
                .Select(candidate => new
                {
                    Candidate = candidate,
                    DistanceSquared = (candidate.LocalPosition - piece.LocalPosition).sqrMagnitude
                })
                .Where(candidate => candidate.DistanceSquared <= StructuralDependencyRadius * StructuralDependencyRadius)
                .OrderBy(candidate => candidate.DistanceSquared)
                .ThenBy(candidate => candidate.Candidate.BuildOrder)
                .Take(6)
                .Select(candidate => candidate.Candidate.PieceId)
                .ToList();

            piece.DependencyPieceIds.AddRange(dependencies);
        }
    }
}
