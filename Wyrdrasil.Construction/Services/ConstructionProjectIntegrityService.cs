using System.Linq;
using UnityEngine;
using Wyrdrasil.Construction.Diagnostics;
using Wyrdrasil.Construction.Models;

namespace Wyrdrasil.Construction.Services;

public sealed class ConstructionProjectIntegrityService
{
    private const float CheckIntervalSeconds = 1.0f;

    private readonly BlueprintCatalogService _blueprintCatalogService;
    private readonly ConstructionProjectService _constructionProjectService;
    private readonly ConstructionDebugLogService _debugLogService;
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
        if (Time.time < _nextCheckTime)
        {
            return;
        }

        _nextCheckTime = Time.time + CheckIntervalSeconds;

        foreach (var project in _constructionProjectService.Projects.ToList())
        {
            if (!_blueprintCatalogService.TryGetBlueprint(project.BlueprintId, out var blueprint))
            {
                continue;
            }

            _constructionProjectService.EnsurePieceProgress(project, blueprint);
            foreach (var pieceProgress in project.Progress.Pieces.ToList())
            {
                if (pieceProgress.State != ConstructionPieceBuildState.Built)
                {
                    continue;
                }

                var piece = blueprint.Pieces.FirstOrDefault(candidate => candidate.PieceId == pieceProgress.PieceId);
                if (piece == null)
                {
                    continue;
                }

                var expectedName = string.IsNullOrWhiteSpace(pieceProgress.LinkedWorldObjectName)
                    ? _constructionProjectService.GetExpectedBuiltPieceObjectName(project.Id, piece.PieceId, piece.PrefabName)
                    : pieceProgress.LinkedWorldObjectName;

                if (string.IsNullOrWhiteSpace(expectedName) || GameObject.Find(expectedName) != null)
                {
                    continue;
                }

                if (_constructionProjectService.TryMarkPieceMissing(project.Id, piece.PieceId, out var changed) && changed)
                {
                    _debugLogService.Info(
                        "Integrity",
                        $"Project {project.Id} piece {piece.PieceId} was destroyed or removed. It has been returned to the construction queue.");
                }
            }
        }
    }
}
