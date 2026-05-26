using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using Wyrdrasil.Construction.Diagnostics;
using Wyrdrasil.Construction.Models;
using Wyrdrasil.Core.Diagnostics;

namespace Wyrdrasil.Construction.Services;

public sealed class ConstructionPieceBuildService
{
    private readonly BlueprintCatalogService _blueprintCatalogService;
    private readonly ConstructionPlacementService _constructionPlacementService;
    private readonly ConstructionProjectService _constructionProjectService;
    private readonly ConstructionPieceBuildOrderService _constructionPieceBuildOrderService;
    private readonly ConstructionDebugLogService _debugLogService;
    private readonly Dictionary<int, CachedProjectPlacements> _placementCacheByProjectId = new();

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

                if (!TryGetResolvedPlacements(project, blueprint, out var cachedPlacements, out failureReason))
                {
                    return false;
                }

                using (WyrdrasilProfiler.Sample("Construction.Build.SelectNextBuildablePiece"))
                {
                    if (!_constructionPieceBuildOrderService.TrySelectNextBuildablePiece(
                            project,
                            blueprint,
                            cachedPlacements.Placements,
                            cachedPlacements.PlacementsByPieceId,
                            cachedPlacements.LowestLocalY,
                            out var placement,
                            out var probeResult,
                            out failureReason))
                    {
                        return false;
                    }

                    GameObject instance;
                    using (WyrdrasilProfiler.Sample("Construction.Build.InstantiatePiece"))
                    {
                        instance = Object.Instantiate(placement.Prefab, placement.WorldPosition, placement.WorldRotation);
                    }

                    var worldObjectName = _constructionProjectService.GetExpectedBuiltPieceObjectName(projectId, placement.PieceId, placement.PrefabName);
                    instance.name = worldObjectName;

                    pieceId = placement.PieceId;
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

    private bool TryGetResolvedPlacements(
        ConstructionProjectData project,
        StructureBlueprintData blueprint,
        out CachedProjectPlacements cachedPlacements,
        out string failureReason)
    {
        using (WyrdrasilProfiler.Sample("Construction.Build.ResolveBlueprintPlacements"))
        {
            if (_placementCacheByProjectId.TryGetValue(project.Id, out cachedPlacements) &&
                cachedPlacements.Matches(project, blueprint))
            {
                failureReason = string.Empty;
                return true;
            }

            if (!_constructionPlacementService.TryResolveBlueprintPlacements(
                    blueprint,
                    project.OriginPosition,
                    project.OriginRotation,
                    out var placements,
                    out failureReason))
            {
                _placementCacheByProjectId.Remove(project.Id);
                cachedPlacements = CachedProjectPlacements.Empty;
                return false;
            }

            cachedPlacements = CachedProjectPlacements.Create(project, blueprint, placements);
            _placementCacheByProjectId[project.Id] = cachedPlacements;
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class CachedProjectPlacements
    {
        public static readonly CachedProjectPlacements Empty = new(
            0,
            string.Empty,
            0,
            Vector3.zero,
            Quaternion.identity,
            new List<ConstructionResolvedPiecePlacement>(),
            new Dictionary<int, ConstructionResolvedPiecePlacement>(),
            0f);

        private readonly int _projectId;
        private readonly string _blueprintId;
        private readonly int _blueprintPieceCount;
        private readonly Vector3 _originPosition;
        private readonly Quaternion _originRotation;

        private CachedProjectPlacements(
            int projectId,
            string blueprintId,
            int blueprintPieceCount,
            Vector3 originPosition,
            Quaternion originRotation,
            IReadOnlyList<ConstructionResolvedPiecePlacement> placements,
            IReadOnlyDictionary<int, ConstructionResolvedPiecePlacement> placementsByPieceId,
            float lowestLocalY)
        {
            _projectId = projectId;
            _blueprintId = blueprintId;
            _blueprintPieceCount = blueprintPieceCount;
            _originPosition = originPosition;
            _originRotation = originRotation;
            Placements = placements;
            PlacementsByPieceId = placementsByPieceId;
            LowestLocalY = lowestLocalY;
        }

        public IReadOnlyList<ConstructionResolvedPiecePlacement> Placements { get; }
        public IReadOnlyDictionary<int, ConstructionResolvedPiecePlacement> PlacementsByPieceId { get; }
        public float LowestLocalY { get; }

        public static CachedProjectPlacements Create(
            ConstructionProjectData project,
            StructureBlueprintData blueprint,
            IReadOnlyList<ConstructionResolvedPiecePlacement> placements)
        {
            var placementsByPieceId = new Dictionary<int, ConstructionResolvedPiecePlacement>();
            foreach (var placement in placements)
            {
                placementsByPieceId[placement.PieceId] = placement;
            }

            var lowestLocalY = 0f;
            if (blueprint.Pieces.Count > 0)
            {
                lowestLocalY = blueprint.Pieces[0].LocalPosition.y;
                for (var index = 1; index < blueprint.Pieces.Count; index++)
                {
                    var y = blueprint.Pieces[index].LocalPosition.y;
                    if (y < lowestLocalY)
                    {
                        lowestLocalY = y;
                    }
                }
            }

            return new CachedProjectPlacements(
                project.Id,
                project.BlueprintId,
                blueprint.Pieces.Count,
                project.OriginPosition,
                project.OriginRotation,
                new List<ConstructionResolvedPiecePlacement>(placements),
                placementsByPieceId,
                lowestLocalY);
        }

        public bool Matches(ConstructionProjectData project, StructureBlueprintData blueprint)
        {
            return _projectId == project.Id &&
                   _blueprintId == project.BlueprintId &&
                   _blueprintPieceCount == blueprint.Pieces.Count &&
                   _originPosition == project.OriginPosition &&
                   _originRotation == project.OriginRotation;
        }
    }
}
