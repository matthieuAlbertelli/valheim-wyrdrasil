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
    private readonly ConstructionPieceBuildOrderService _constructionPieceBuildOrderService;
    private readonly ConstructionDebugLogService _debugLogService;

    public ConstructionPieceBuildService(
        BlueprintCatalogService blueprintCatalogService,
        ConstructionPlacementService constructionPlacementService,
        ConstructionProjectService constructionProjectService,
        ConstructionPieceBuildOrderService constructionPieceBuildOrderService,
        ConstructionDebugLogService debugLogService)
    {
        _blueprintCatalogService = blueprintCatalogService;
        _constructionPlacementService = constructionPlacementService;
        _constructionProjectService = constructionProjectService;
        _constructionPieceBuildOrderService = constructionPieceBuildOrderService;
        _debugLogService = debugLogService;
    }

    public bool TryBuildNextPiece(
        int projectId,
        out int pieceId,
        out int builtPieceCount,
        out bool isCompleted,
        out string failureReason)
    {
        pieceId = 0;
        builtPieceCount = 0;
        isCompleted = false;

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

        _constructionProjectService.EnsurePieceProgress(project, blueprint);
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

        if (!_constructionPieceBuildOrderService.TrySelectNextBuildablePiece(
                project,
                blueprint,
                placements,
                out var placement,
                out var probeResult,
                out failureReason))
        {
            return false;
        }

        var instance = Object.Instantiate(placement.Prefab, placement.WorldPosition, placement.WorldRotation);
        var worldObjectName = _constructionProjectService.GetExpectedBuiltPieceObjectName(projectId, placement.PieceId, placement.PrefabName);
        instance.name = worldObjectName;

        pieceId = placement.PieceId;
        if (!_constructionProjectService.TryMarkPieceBuilt(
                projectId,
                pieceId,
                worldObjectName,
                probeResult.Level,
                out builtPieceCount,
                out isCompleted))
        {
            failureReason = $"Could not mark piece {pieceId} as built for construction project {projectId}.";
            return false;
        }

        failureReason = string.Empty;
        _debugLogService.Verbose(
            "Build",
            $"Built piece {pieceId} for construction project {projectId} at {placement.WorldPosition}. Stability={probeResult.Level}. Reason={probeResult.Reason}");
        return true;
    }

    public bool TryBuildNextPiece(int projectId, out int pieceId, out string failureReason)
    {
        return TryBuildNextPiece(projectId, out pieceId, out _, out _, out failureReason);
    }
}
