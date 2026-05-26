using System.Collections.Generic;
using System.Linq;
using Wyrdrasil.Construction.Diagnostics;
using Wyrdrasil.Construction.Models;

namespace Wyrdrasil.Construction.Services;

public sealed class ConstructionProjectProgressService
{
    private const float PieceRatePerWorkerPerGameHour = 30f;

    // Building a Valheim piece is not a cheap data mutation: it instantiates a real prefab,
    // creates the corresponding networked scene object, then updates project state. Even with
    // a high logical work rate, the runtime should consume completed work progressively instead
    // of placing an entire catch-up burst in one Unity frame.
    private const int MaxPiecesBuiltPerUpdate = 1;

    private readonly ConstructionGameTimeService _constructionGameTimeService;
    private readonly ConstructionProjectService _constructionProjectService;
    private readonly ConstructionPieceBuildService _constructionPieceBuildService;
    private readonly ConstructionDebugLogService _debugLogService;
    private int _nextBuildProjectIndex;

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

        if (activeProjects.Count == 0)
        {
            _nextBuildProjectIndex = 0;
            return;
        }

        AccumulateWorkerProgress(activeProjects, deltaGameHours);
        BuildReadyPiecesWithinFrameBudget(activeProjects);
    }

    private void AccumulateWorkerProgress(IEnumerable<ConstructionProjectData> activeProjects, float deltaGameHours)
    {
        foreach (var project in activeProjects)
        {
            var activeWorkerCount = _constructionProjectService.GetActiveWorkerCount(project.Id);
            if (activeWorkerCount <= 0)
            {
                continue;
            }

            var workAmount = activeWorkerCount * PieceRatePerWorkerPerGameHour * deltaGameHours;
            _constructionProjectService.TryAddAccumulatedPieceWork(project.Id, workAmount, out _);
        }
    }

    private void BuildReadyPiecesWithinFrameBudget(IReadOnlyList<ConstructionProjectData> activeProjects)
    {
        if (activeProjects.Count == 0)
        {
            return;
        }

        if (_nextBuildProjectIndex < 0 || _nextBuildProjectIndex >= activeProjects.Count)
        {
            _nextBuildProjectIndex = 0;
        }

        var inspectedProjectCount = 0;
        var builtPieceCountThisUpdate = 0;

        while (inspectedProjectCount < activeProjects.Count && builtPieceCountThisUpdate < MaxPiecesBuiltPerUpdate)
        {
            var projectIndex = (_nextBuildProjectIndex + inspectedProjectCount) % activeProjects.Count;
            var project = activeProjects[projectIndex];
            inspectedProjectCount++;

            if (!_constructionProjectService.TryConsumeOnePieceWork(project.Id, out var remainingAccumulatedWork))
            {
                continue;
            }

            builtPieceCountThisUpdate++;
            _nextBuildProjectIndex = (projectIndex + 1) % activeProjects.Count;

            if (!_constructionPieceBuildService.TryBuildNextPiece(
                    project.Id,
                    out var pieceId,
                    out var builtPieceCount,
                    out var isCompleted,
                    out var failureReason))
            {
                _constructionProjectService.TrySetState(project.Id, ConstructionProjectState.Blocked);
                _debugLogService.Warning(
                    "Progress",
                    $"Blocked construction project {project.Id} while building the next safe piece: {failureReason}");
                continue;
            }

            _debugLogService.Verbose(
                "Progress",
                $"Construction project {project.Id} built piece {pieceId}. Progress: {builtPieceCount}/{project.Progress.TotalPieceCount}. Remaining accumulated work: {remainingAccumulatedWork:0.##}.");

            if (isCompleted)
            {
                _debugLogService.Info("Progress", $"Construction project {project.Id} is now completed.");
            }
        }
    }
}
