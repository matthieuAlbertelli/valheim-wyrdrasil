using UnityEngine;
using Object = UnityEngine.Object;
using Wyrdrasil.Construction.Diagnostics;
using Wyrdrasil.Construction.Models;

namespace Wyrdrasil.Construction.Services;

public sealed class ConstructionPieceBuildService
{
    private readonly BlueprintCatalogService _blueprintCatalogService;
    private readonly ConstructionPlacementService _constructionPlacementService;
    private readonly ConstructionProjectService _constructionProjectService;
    private readonly ConstructionDebugLogService _debugLogService;

    public ConstructionPieceBuildService(
        BlueprintCatalogService blueprintCatalogService,
        ConstructionPlacementService constructionPlacementService,
        ConstructionProjectService constructionProjectService,
        ConstructionDebugLogService debugLogService)
    {
        _blueprintCatalogService = blueprintCatalogService;
        _constructionPlacementService = constructionPlacementService;
        _constructionProjectService = constructionProjectService;
        _debugLogService = debugLogService;
    }

    public bool TryBuildNextPiece(int projectId, out int pieceId, out string failureReason)
    {
        pieceId = 0;

        if (!_constructionProjectService.TryGetProject(projectId, out var project))
        {
            failureReason = $"Unknown construction project {projectId}.";
            return false;
        }

        if (!_blueprintCatalogService.TryGetBlueprint(project.BlueprintId, out var blueprint))
        {
            failureReason = $"Unknown blueprint '{project.BlueprintId}' for construction project {projectId}.";
            return false;
        }

        if (project.Progress.BuiltPieceCount >= project.Progress.TotalPieceCount)
        {
            failureReason = $"Construction project {projectId} is already complete.";
            return false;
        }

        if (!_constructionPlacementService.TryResolveBlueprintPlacements(
                blueprint,
                project.OriginPosition,
                project.OriginRotation,
                out var placements,
                out failureReason))
        {
            return false;
        }

        var pieceIndex = project.Progress.BuiltPieceCount;
        if (pieceIndex < 0 || pieceIndex >= placements.Count)
        {
            failureReason = $"Construction project {projectId} could not resolve piece index {pieceIndex}.";
            return false;
        }

        var placement = placements[pieceIndex];
        var instance = Object.Instantiate(placement.Prefab, placement.WorldPosition, placement.WorldRotation);
        instance.name = $"{placement.Prefab.name}_ConstructionProject_{projectId}_Piece_{placement.PieceId}";

        pieceId = placement.PieceId;
        failureReason = string.Empty;
        _debugLogService.Verbose(
            "Build",
            $"Built piece {pieceId} for construction project {projectId} at {placement.WorldPosition}.");
        return true;
    }
}
