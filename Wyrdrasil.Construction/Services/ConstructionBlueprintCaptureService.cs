using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Wyrdrasil.Construction.Models;
using Wyrdrasil.Settlements.Tool;

namespace Wyrdrasil.Construction.Services;

public sealed class ConstructionBlueprintCaptureService
{
    public bool TryCaptureFromZone(
        FunctionalZoneData zone,
        Vector3 originPosition,
        string blueprintId,
        string displayName,
        out StructureBlueprintData blueprint,
        out string failureReason)
    {
        var pieces = UnityEngine.Object.FindObjectsByType<Piece>(FindObjectsSortMode.None)
            .Where(candidate => candidate != null && candidate.gameObject != null && zone.ContainsPoint(candidate.transform.position))
            .OrderBy(candidate => candidate.transform.position.y)
            .ThenBy(candidate => candidate.transform.position.x)
            .ThenBy(candidate => candidate.transform.position.z)
            .ToList();

        if (pieces.Count == 0)
        {
            blueprint = new StructureBlueprintData();
            failureReason = $"No build pieces were found inside zone #{zone.Id}.";
            return false;
        }

        var capturedPieces = new List<BlueprintPieceData>(pieces.Count);
        for (var index = 0; index < pieces.Count; index++)
        {
            var piece = pieces[index];
            capturedPieces.Add(new BlueprintPieceData
            {
                PieceId = index + 1,
                PrefabName = ExtractPrefabName(piece.gameObject.name),
                LocalPosition = piece.transform.position - originPosition,
                LocalRotation = piece.transform.rotation,
                Materials = CaptureMaterials(piece)
            });
        }

        blueprint = new StructureBlueprintData
        {
            Id = string.IsNullOrWhiteSpace(blueprintId) ? Guid.NewGuid().ToString("N") : blueprintId,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? $"Captured Zone {zone.Id}" : displayName,
            Pieces = capturedPieces
        };

        failureReason = string.Empty;
        return true;
    }

    private static List<MaterialRequirementData> CaptureMaterials(Piece piece)
    {
        var result = new List<MaterialRequirementData>();
        if (piece.m_resources == null)
        {
            return result;
        }

        foreach (var requirement in piece.m_resources)
        {
            if (requirement == null || requirement.m_resItem == null || requirement.m_amount <= 0)
            {
                continue;
            }

            result.Add(new MaterialRequirementData
            {
                ItemPrefabName = ExtractPrefabName(requirement.m_resItem.gameObject.name),
                Amount = requirement.m_amount
            });
        }

        return result;
    }

    private static string ExtractPrefabName(string gameObjectName)
    {
        if (string.IsNullOrWhiteSpace(gameObjectName))
        {
            return string.Empty;
        }

        const string cloneSuffix = "(Clone)";
        if (gameObjectName.EndsWith(cloneSuffix, StringComparison.Ordinal))
        {
            gameObjectName = gameObjectName.Substring(0, gameObjectName.Length - cloneSuffix.Length);
        }

        return gameObjectName.Trim();
    }
}
