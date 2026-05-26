using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Wyrdrasil.Construction.Diagnostics;
using Wyrdrasil.Construction.Models;
using Wyrdrasil.Core.Diagnostics;

namespace Wyrdrasil.Construction.Services;

public sealed class ConstructionProjectIntegrityService
{
    private const float CheckIntervalSeconds = 1.0f;
    private const int MaxPieceChecksPerProjectTick = 32;
    private const int MaxPieceIdsInLog = 8;

    private readonly BlueprintCatalogService _blueprintCatalogService;
    private readonly ConstructionProjectPlacementCacheService _projectPlacementCacheService;
    private readonly ConstructionWorldPieceIndexService _worldPieceIndexService;
    private readonly ConstructionProjectService _constructionProjectService;
    private readonly ConstructionDebugLogService _debugLogService;
    private readonly Dictionary<int, int> _nextPieceProgressIndexByProjectId = new();
    private float _nextCheckTime;

    public ConstructionProjectIntegrityService(
        BlueprintCatalogService blueprintCatalogService,
        ConstructionProjectPlacementCacheService projectPlacementCacheService,
        ConstructionWorldPieceIndexService worldPieceIndexService,
        ConstructionProjectService constructionProjectService,
        ConstructionDebugLogService debugLogService)
    {
        _blueprintCatalogService = blueprintCatalogService;
        _projectPlacementCacheService = projectPlacementCacheService;
        _worldPieceIndexService = worldPieceIndexService;
        _constructionProjectService = constructionProjectService;
        _debugLogService = debugLogService;
    }

    public void Update()
    {
        using (WyrdrasilProfiler.Sample("Construction.Integrity.Update"))
        {
            if (Time.time < _nextCheckTime)
            {
                return;
            }

            _nextCheckTime = Time.time + CheckIntervalSeconds;

            foreach (var project in _constructionProjectService.Projects.ToList())
            {
                CheckProject(project);
            }
        }
    }

    private void CheckProject(ConstructionProjectData project)
    {
        using (WyrdrasilProfiler.Sample("Construction.Integrity.CheckProject"))
        {
            if (!_blueprintCatalogService.TryGetBlueprint(project.BlueprintId, out var blueprint))
            {
                return;
            }

            _constructionProjectService.EnsurePieceProgress(project, blueprint);
            if (project.Progress.Pieces.Count == 0)
            {
                _nextPieceProgressIndexByProjectId[project.Id] = 0;
                return;
            }

            if (!_projectPlacementCacheService.TryGetResolvedPlacements(project, blueprint, out var cachedPlacements, out var failureReason))
            {
                _debugLogService.Warning("Integrity", $"Could not resolve placements for construction project {project.Id}: {failureReason}");
                return;
            }

            var worldPieceIndex = _worldPieceIndexService.GetOrBuildProjectIndex(project, cachedPlacements.Placements);
            var checkedPieceCount = 0;
            var changedMissingPieceIds = new List<int>();
            var adoptedPieceIds = new List<int>();
            var startIndex = GetStartIndex(project);
            var index = startIndex;

            while (checkedPieceCount < project.Progress.Pieces.Count && checkedPieceCount < MaxPieceChecksPerProjectTick)
            {
                if (index >= project.Progress.Pieces.Count)
                {
                    index = 0;
                }

                var pieceProgress = project.Progress.Pieces[index];
                index++;
                checkedPieceCount++;

                if (!cachedPlacements.PlacementsByPieceId.TryGetValue(pieceProgress.PieceId, out var placement))
                {
                    continue;
                }

                if (worldPieceIndex.TryFindMatchingPiece(placement, out var match))
                {
                    if (pieceProgress.State != ConstructionPieceBuildState.Built)
                    {
                        using (WyrdrasilProfiler.Sample("Construction.Integrity.AdoptExistingPiece"))
                        {
                            if (!_worldPieceIndexService.AdoptExistingPiece(project, placement, match))
                            {
                                continue;
                            }

                            if (_constructionProjectService.TryMarkPieceBuilt(
                                    project.Id,
                                    pieceProgress.PieceId,
                                    match.WorldObjectName,
                                    ConstructionPieceStabilityLevel.Acceptable,
                                    out _,
                                    out _))
                            {
                                adoptedPieceIds.Add(pieceProgress.PieceId);
                            }
                        }
                    }

                    continue;
                }

                if (pieceProgress.State != ConstructionPieceBuildState.Built)
                {
                    continue;
                }

                if (_constructionProjectService.TryMarkPieceMissing(project.Id, pieceProgress.PieceId, out var changed) && changed)
                {
                    changedMissingPieceIds.Add(pieceProgress.PieceId);
                }
            }

            _nextPieceProgressIndexByProjectId[project.Id] = index >= project.Progress.Pieces.Count ? 0 : index;
            LogPieceChangesIfNeeded(project.Id, changedMissingPieceIds, adoptedPieceIds);
        }
    }

    private int GetStartIndex(ConstructionProjectData project)
    {
        if (!_nextPieceProgressIndexByProjectId.TryGetValue(project.Id, out var startIndex))
        {
            return 0;
        }

        if (startIndex < 0 || startIndex >= project.Progress.Pieces.Count)
        {
            return 0;
        }

        return startIndex;
    }

    private void LogPieceChangesIfNeeded(
        int projectId,
        IReadOnlyList<int> changedMissingPieceIds,
        IReadOnlyList<int> adoptedPieceIds)
    {
        if (changedMissingPieceIds.Count > 0)
        {
            var visibleIds = string.Join(", ", changedMissingPieceIds.Take(MaxPieceIdsInLog));
            var suffix = changedMissingPieceIds.Count > MaxPieceIdsInLog ? ", ..." : string.Empty;
            _debugLogService.Info(
                "Integrity",
                $"Project {projectId} detected {changedMissingPieceIds.Count} missing built piece(s) from the native piece graph. Returned to construction queue: {visibleIds}{suffix}.");
        }

        if (adoptedPieceIds.Count > 0)
        {
            var visibleIds = string.Join(", ", adoptedPieceIds.Take(MaxPieceIdsInLog));
            var suffix = adoptedPieceIds.Count > MaxPieceIdsInLog ? ", ..." : string.Empty;
            _debugLogService.Info(
                "Integrity",
                $"Project {projectId} adopted {adoptedPieceIds.Count} existing native piece(s) matching the blueprint: {visibleIds}{suffix}.");
        }
    }
}
