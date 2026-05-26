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
    private const int MaxBuiltPieceChecksPerProjectTick = 32;
    private const int MaxWorldObjectLookupsPerIntegrityTick = 1;
    private const int MaxMissingPieceIdsInLog = 8;

    private readonly BlueprintCatalogService _blueprintCatalogService;
    private readonly ConstructionProjectService _constructionProjectService;
    private readonly ConstructionDebugLogService _debugLogService;
    private readonly Dictionary<int, int> _nextPieceProgressIndexByProjectId = new();
    private readonly Dictionary<string, GameObject> _worldObjectCacheByName = new();
    private int _remainingWorldObjectLookupsThisTick;
    private float _nextCheckTime;

    public ConstructionProjectIntegrityService(
        BlueprintCatalogService blueprintCatalogService,
        ConstructionProjectService constructionProjectService,
        ConstructionDebugLogService debugLogService)
    {
        _blueprintCatalogService = blueprintCatalogService;
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
            _remainingWorldObjectLookupsThisTick = MaxWorldObjectLookupsPerIntegrityTick;

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

            var pieceById = blueprint.Pieces.ToDictionary(candidate => candidate.PieceId);
            var checkedPieceCount = 0;
            var changedMissingPieceIds = new List<int>();
            var startIndex = GetStartIndex(project);
            var index = startIndex;

            while (checkedPieceCount < project.Progress.Pieces.Count && checkedPieceCount < MaxBuiltPieceChecksPerProjectTick)
            {
                if (index >= project.Progress.Pieces.Count)
                {
                    index = 0;
                }

                var pieceProgress = project.Progress.Pieces[index];
                index++;
                checkedPieceCount++;

                if (pieceProgress.State != ConstructionPieceBuildState.Built)
                {
                    continue;
                }

                if (!pieceById.TryGetValue(pieceProgress.PieceId, out var piece))
                {
                    continue;
                }

                var expectedName = string.IsNullOrWhiteSpace(pieceProgress.LinkedWorldObjectName)
                    ? _constructionProjectService.GetExpectedBuiltPieceObjectName(project.Id, piece.PieceId, piece.PrefabName)
                    : pieceProgress.LinkedWorldObjectName;

                if (string.IsNullOrWhiteSpace(expectedName))
                {
                    continue;
                }

                if (TryWorldObjectStillExists(expectedName, out var exists, out var deferred) && exists)
                {
                    continue;
                }

                if (deferred)
                {
                    continue;
                }

                if (_constructionProjectService.TryMarkPieceMissing(project.Id, piece.PieceId, out var changed) && changed)
                {
                    changedMissingPieceIds.Add(piece.PieceId);
                }
            }

            _nextPieceProgressIndexByProjectId[project.Id] = index >= project.Progress.Pieces.Count ? 0 : index;
            LogMissingPiecesIfNeeded(project.Id, changedMissingPieceIds);
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

    private bool TryWorldObjectStillExists(string expectedName, out bool exists, out bool deferred)
    {
        deferred = false;

        if (_worldObjectCacheByName.TryGetValue(expectedName, out var cachedObject))
        {
            if (cachedObject != null)
            {
                exists = true;
                return true;
            }

            _worldObjectCacheByName.Remove(expectedName);
        }

        if (_remainingWorldObjectLookupsThisTick <= 0)
        {
            exists = false;
            deferred = true;
            return false;
        }

        _remainingWorldObjectLookupsThisTick--;
        using (WyrdrasilProfiler.Sample("Construction.Integrity.FindWorldObject"))
        {
            var foundObject = GameObject.Find(expectedName);
            if (foundObject != null)
            {
                _worldObjectCacheByName[expectedName] = foundObject;
                exists = true;
                return true;
            }
        }

        exists = false;
        return true;
    }

    private void LogMissingPiecesIfNeeded(int projectId, IReadOnlyList<int> changedMissingPieceIds)
    {
        if (changedMissingPieceIds.Count == 0)
        {
            return;
        }

        var visibleIds = string.Join(", ", changedMissingPieceIds.Take(MaxMissingPieceIdsInLog));
        var suffix = changedMissingPieceIds.Count > MaxMissingPieceIdsInLog ? ", ..." : string.Empty;
        _debugLogService.Info(
            "Integrity",
            $"Project {projectId} detected {changedMissingPieceIds.Count} missing built piece(s) during this integrity tick. Returned to construction queue: {visibleIds}{suffix}.");
    }
}
