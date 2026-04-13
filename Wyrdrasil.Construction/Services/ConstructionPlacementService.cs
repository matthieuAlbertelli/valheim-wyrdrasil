using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;
using Wyrdrasil.Construction.Diagnostics;
using Wyrdrasil.Construction.Models;

namespace Wyrdrasil.Construction.Services;

public sealed class ConstructionPlacementService
{
    private readonly ConstructionDebugLogService _debugLogService;

    public ConstructionPlacementService(ConstructionDebugLogService debugLogService)
    {
        _debugLogService = debugLogService;
    }

    public bool TryPlaceBlueprintInstantly(StructureBlueprintData blueprint, Vector3 originPosition, Quaternion originRotation, out int placedPieceCount, out string failureReason)
    {
        placedPieceCount = 0;

        if (!TryResolveBlueprintPlacements(blueprint, originPosition, originRotation, out var placements, out failureReason))
        {
            return false;
        }

        foreach (var placement in placements)
        {
            var instance = Object.Instantiate(placement.Prefab, placement.WorldPosition, placement.WorldRotation);
            instance.name = $"{placement.Prefab.name}_ConstructionDebug";
            placedPieceCount++;

            _debugLogService.Verbose("Placement", $"Placed prefab '{placement.Prefab.name}' for piece {placement.PieceId} at {placement.WorldPosition}.");
        }

        failureReason = string.Empty;
        _debugLogService.Info("Placement", $"Instantly placed blueprint '{blueprint.Id}' with {placedPieceCount} pieces.");
        return true;
    }

    public bool TryResolveBlueprintPlacements(
        StructureBlueprintData blueprint,
        Vector3 originPosition,
        Quaternion originRotation,
        out List<ConstructionResolvedPiecePlacement> placements,
        out string failureReason)
    {
        placements = new List<ConstructionResolvedPiecePlacement>();

        if (ZNetScene.instance == null)
        {
            failureReason = "ZNetScene.instance is not available.";
            return false;
        }

        foreach (var piece in blueprint.Pieces.OrderBy(candidate => candidate.BuildOrder).ThenBy(candidate => candidate.PieceId))
        {
            var prefab = ResolvePrefab(piece.PrefabName);
            if (prefab == null)
            {
                failureReason = $"Could not resolve prefab '{piece.PrefabName}' for blueprint '{blueprint.Id}'.";
                _debugLogService.Warning("Placement", failureReason);
                return false;
            }

            var localRotation = NormalizeRotation(piece.LocalRotation);
            var worldPosition = originPosition + (originRotation * piece.LocalPosition);
            var worldRotation = originRotation * localRotation;

            placements.Add(new ConstructionResolvedPiecePlacement
            {
                PieceId = piece.PieceId,
                PrefabName = piece.PrefabName,
                Prefab = prefab,
                WorldPosition = worldPosition,
                WorldRotation = worldRotation
            });
        }

        failureReason = string.Empty;
        return true;
    }

    private GameObject? ResolvePrefab(string prefabName)
    {
        if (string.IsNullOrWhiteSpace(prefabName))
        {
            return null;
        }

        var exact = ZNetScene.instance.GetPrefab(prefabName);
        if (exact != null)
        {
            return exact;
        }

        var normalizedTarget = NormalizePrefabName(prefabName);
        var match = ZNetScene.instance.m_prefabs.FirstOrDefault(candidate =>
            string.Equals(candidate.name, prefabName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(NormalizePrefabName(candidate.name), normalizedTarget, StringComparison.OrdinalIgnoreCase));

        if (match != null)
        {
            _debugLogService.Verbose("Placement", $"Resolved prefab '{prefabName}' to scene prefab '{match.name}' via normalized lookup.");
        }

        return match;
    }

    private static Quaternion NormalizeRotation(Quaternion rotation)
    {
        var isZeroQuaternion =
            Mathf.Approximately(rotation.x, 0f) &&
            Mathf.Approximately(rotation.y, 0f) &&
            Mathf.Approximately(rotation.z, 0f) &&
            Mathf.Approximately(rotation.w, 0f);

        return isZeroQuaternion ? Quaternion.identity : rotation;
    }

    private static string NormalizePrefabName(string value)
    {
        return new string(value
            .Where(character => character != '_' && character != '-' && !char.IsWhiteSpace(character))
            .ToArray())
            .ToLowerInvariant();
    }
}
