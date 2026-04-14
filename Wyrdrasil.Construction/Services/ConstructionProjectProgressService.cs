using System.Linq;
using Wyrdrasil.Construction.Diagnostics;
using Wyrdrasil.Construction.Models;

namespace Wyrdrasil.Construction.Services;

public sealed class ConstructionProjectProgressService
{
    private const float PieceRatePerWorkerPerGameHour = 10f;

    private readonly ConstructionGameTimeService _constructionGameTimeService;
    private readonly ConstructionProjectService _constructionProjectService;
    private readonly ConstructionPieceBuildService _constructionPieceBuildService;
    private readonly ConstructionDebugLogService _debugLogService;

    public ConstructionProjectProgressService(
        ConstructionGameTimeService constructionGameTimeService,
        ConstructionProjectService constructionProjectService,
        ConstructionPieceBuildService constructionPieceBuildService,
        ConstructionDebugLogService debugLogService)
    {
        _constructionGameTimeService = constructionGameTimeService;
        _constructionProjectService = constructionProjectService;
        _constructionPieceBuildService = constructionPieceBuildService;
        _debugLogService = debugLogService;
    }

    public void Update()
    {
        var deltaGameHours = _constructionGameTimeService.ConsumeDeltaGameHours();
        if (deltaGameHours <= 0f)
        {
            return;
        }

        var activeProjects = _constructionProjectService.Projects
            .Where(project => project.State != ConstructionProjectState.Completed && project.State != ConstructionProjectState.Blocked)
            .OrderBy(project => project.Id)
            .ToList();

        foreach (var project in activeProjects)
        {
            var activeWorkerCount = _constructionProjectService.GetActiveWorkerCount(project.Id);
            if (activeWorkerCount <= 0)
            {
                continue;
            }

            var workAmount = activeWorkerCount * PieceRatePerWorkerPerGameHour * deltaGameHours;
            if (!_constructionProjectService.TryAddAccumulatedPieceWork(project.Id, workAmount, out _))
            {
                continue;
            }

            while (_constructionProjectService.TryConsumeOnePieceWork(project.Id, out var remainingAccumulatedWork))
            {
                if (!_constructionPieceBuildService.TryBuildNextPiece(project.Id, out var pieceId, out var failureReason))
                {
                    _constructionProjectService.TrySetState(project.Id, ConstructionProjectState.Blocked);
                    _debugLogService.Warning(
                        "Progress",
                        $"Blocked construction project {project.Id} while building the next piece: {failureReason}");
                    break;
                }

                _constructionProjectService.TryMarkNextPieceBuilt(project.Id, out var builtPieceCount, out var isCompleted);
                _debugLogService.Info(
                    "Progress",
                    $"Construction project {project.Id} built piece {pieceId}. Progress: {builtPieceCount}/{project.Progress.TotalPieceCount}. Remaining accumulated work: {remainingAccumulatedWork:0.##}.");

                if (isCompleted)
                {
                    _debugLogService.Info("Progress", $"Construction project {project.Id} is now completed.");
                    break;
                }
            }
        }
    }
}
