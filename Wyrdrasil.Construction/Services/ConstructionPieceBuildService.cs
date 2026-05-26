using UnityEngine;
using Object = UnityEngine.Object;
using Wyrdrasil.Construction.Diagnostics;
using Wyrdrasil.Construction.Models;
using Wyrdrasil.Core.Diagnostics;

namespace Wyrdrasil.Construction.Services;

public sealed class ConstructionPieceBuildService
{
    private readonly BlueprintCatalogService _blueprintCatalogService;
    private readonly ConstructionProjectPlacementCacheService _projectPlacementCacheService;
    private readonly ConstructionWorldPieceIndexService _worldPieceIndexService;
    private readonly ConstructionProjectService _constructionProjectService;
    private readonly ConstructionPieceBuildOrderService _constructionPieceBuildOrderService;
    private readonly ConstructionDebugLogService _debugLogService;

    public ConstructionPieceBuildService(
        BlueprintCatalogService blueprintCatalogService,
        ConstructionProjectPlacementCacheService projectPlacementCacheService,
        ConstructionWorldPieceIndexService worldPieceIndexService,
        ConstructionProjectService constructionProjectService,
        ConstructionPieceBuildOrderService constructionPieceBuildOrderService,
        ConstructionDebugLogService debugLogService)
    {
        _blueprintCatalogService = blueprintCatalogService;
        _projectPlacementCacheService = projectPlacementCacheService;
        _worldPieceIndexService = worldPieceIndexService;
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
        using (WyrdrasilProfiler.Sample("Construction.Build.TryBuildNextPiece"))
        {
            pieceId = 0;
            builtPieceCount = 0;
            isCompleted = false;

            if (!_constructionProjectService.TryGetProject(projectId, out var project))
            {
                failureReason = $"Unknown construction project {projectId}.";
                return false;
            }

            using (WyrdrasilProfiler.Sample("Construction.Build.TryGetBlueprint"))
            {
                if (!_blueprintCatalogService.TryGetBlueprint(project.BlueprintId, out var blueprint))
                {
                    failureReason = $"Unknown blueprint '{project.BlueprintId}' for construction project {projectId}.";
                    return false;
                }

                using (WyrdrasilProfiler.Sample("Construction.Build.EnsurePieceProgress"))
                {
                    _constructionProjectService.EnsurePieceProgress(project, blueprint);
                }

                if (project.Progress.BuiltPieceCount >= project.Progress.TotalPieceCount)
                {
                    failureReason = $"Construction project {projectId} is already complete.";
                    return false;
                }

                if (!_projectPlacementCacheService.TryGetResolvedPlacements(project, blueprint, out var cachedPlacements, out failureReason))
                {
                    return false;
                }

                var worldPieceIndex = _worldPieceIndexService.GetOrBuildProjectIndex(project, cachedPlacements.Placements);

                using (WyrdrasilProfiler.Sample("Construction.Build.SelectNextBuildablePiece"))
                {
                    if (!_constructionPieceBuildOrderService.TrySelectNextBuildablePiece(
                            project,
                            blueprint,
                            cachedPlacements.Placements,
                            cachedPlacements.PlacementsByPieceId,
                            cachedPlacements.LowestLocalY,
                            worldPieceIndex,
                            out var placement,
                            out var probeResult,
                            out failureReason))
                    {
                        return false;
                    }

                    pieceId = placement.PieceId;
                    if (worldPieceIndex.TryFindMatchingPiece(placement, out var existingMatch))
                    {
                        using (WyrdrasilProfiler.Sample("Construction.Build.AdoptExistingPiece"))
                        {
                            if (!_worldPieceIndexService.AdoptExistingPiece(project, placement, existingMatch))
                            {
                                failureReason = $"Existing piece {pieceId} for construction project {projectId} is already claimed by another construction project.";
                                return false;
                            }

                            if (!_constructionProjectService.TryMarkPieceBuilt(
                                    projectId,
                                    pieceId,
                                    existingMatch.WorldObjectName,
                                    probeResult.Level,
                                    out builtPieceCount,
                                    out isCompleted))
                            {
                                failureReason = $"Could not adopt existing piece {pieceId} for construction project {projectId}.";
                                return false;
                            }
                        }

                        failureReason = string.Empty;
                        _debugLogService.Verbose(
                            "Build",
                            $"Adopted existing world piece '{existingMatch.WorldObjectName}' as construction project {projectId} piece {pieceId}. Stability={probeResult.Level}. Reason={probeResult.Reason}");
                        return true;
                    }

                    GameObject instance;
                    using (WyrdrasilProfiler.Sample("Construction.Build.InstantiatePiece"))
                    {
                        instance = Object.Instantiate(placement.Prefab, placement.WorldPosition, placement.WorldRotation);
                    }

                    var worldObjectName = _constructionProjectService.GetExpectedBuiltPieceObjectName(projectId, placement.PieceId, placement.PrefabName);
                    instance.name = worldObjectName;
                    _worldPieceIndexService.RegisterBuiltPiece(project, placement, instance);

                    using (WyrdrasilProfiler.Sample("Construction.Build.MarkPieceBuilt"))
                    {
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
                    }

                    failureReason = string.Empty;
                    _debugLogService.Verbose(
                        "Build",
                        $"Built piece {pieceId} for construction project {projectId} at {placement.WorldPosition}. Stability={probeResult.Level}. Reason={probeResult.Reason}");
                    return true;
                }
            }
        }
    }

    public bool TryBuildNextPiece(int projectId, out int pieceId, out string failureReason)
    {
        return TryBuildNextPiece(projectId, out pieceId, out _, out _, out failureReason);
    }
}
