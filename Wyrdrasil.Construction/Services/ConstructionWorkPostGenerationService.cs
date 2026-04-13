using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Wyrdrasil.Construction.Models;

namespace Wyrdrasil.Construction.Services;

public sealed class ConstructionWorkPostGenerationService
{
    private const float ExteriorMargin = 3f;

    public List<ConstructionWorkPostData> GenerateWorkPosts(
        int projectId,
        StructureBlueprintData blueprint,
        Vector3 originPosition,
        Quaternion originRotation,
        System.Func<int> allocateWorkPostId)
    {
        var orderedPieces = blueprint.Pieces
            .OrderBy(piece => piece.BuildOrder)
            .ThenBy(piece => piece.PieceId)
            .ToList();

        if (orderedPieces.Count == 0)
        {
            return new List<ConstructionWorkPostData>();
        }

        var minX = orderedPieces.Min(piece => piece.LocalPosition.x);
        var maxX = orderedPieces.Max(piece => piece.LocalPosition.x);
        var minY = orderedPieces.Min(piece => piece.LocalPosition.y);
        var minZ = orderedPieces.Min(piece => piece.LocalPosition.z);
        var maxZ = orderedPieces.Max(piece => piece.LocalPosition.z);

        var localCenter = new Vector3((minX + maxX) * 0.5f, minY, (minZ + maxZ) * 0.5f);
        var localPositions = new[]
        {
            new Vector3(localCenter.x, minY, maxZ + ExteriorMargin),
            new Vector3(localCenter.x, minY, minZ - ExteriorMargin),
            new Vector3(maxX + ExteriorMargin, minY, localCenter.z),
            new Vector3(minX - ExteriorMargin, minY, localCenter.z)
        };

        var worldCenter = originPosition + (originRotation * localCenter);
        var workPosts = new List<ConstructionWorkPostData>(localPositions.Length);
        foreach (var localPosition in localPositions)
        {
            var worldPosition = originPosition + (originRotation * localPosition);
            var lookDirection = worldCenter - worldPosition;
            lookDirection.y = 0f;

            var worldRotation = lookDirection.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(lookDirection.normalized, Vector3.up)
                : originRotation;

            workPosts.Add(new ConstructionWorkPostData
            {
                Id = allocateWorkPostId(),
                ProjectId = projectId,
                WorldPosition = worldPosition,
                WorldRotation = worldRotation
            });
        }

        return workPosts;
    }
}
