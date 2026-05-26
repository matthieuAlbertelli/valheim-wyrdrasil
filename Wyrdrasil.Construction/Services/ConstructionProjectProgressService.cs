using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Wyrdrasil.Construction.Diagnostics;
using Wyrdrasil.Construction.Models;
using Wyrdrasil.Core.Diagnostics;

namespace Wyrdrasil.Construction.Services;

public sealed class ConstructionProjectProgressService
{
    private const float PieceRatePerWorkerPerGameHour = 30f;

    // Building a Valheim piece is not a cheap data mutation: it instantiates a real prefab,
    // creates the corresponding networked scene object, then updates project state. Even with
    // a high logical work rate, the runtime should consume completed work progressively instead
    // of placing an entire catch-up burst in one Unity frame.
    private const int MaxPiecesBuiltPerUpdate = 1;
    private const double ProfilerReportIntervalSeconds = 5.0;
    private const string ProfilerScope = "Construction.";

    private readonly ConstructionGameTimeService _constructionGameTimeService;
    private readonly ConstructionProjectService _constructionProjectService;
    private readonly ConstructionPieceBuildService _constructionPieceBuildService;
    private readonly ConstructionDebugLogService _debugLogService;
    private readonly Stopwatch _profilerReportTimer = Stopwatch.StartNew();
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
        using (WyrdrasilProfiler.Sample("Construction.Progress.Update"))
        {
            var deltaGameHours = ConsumeDeltaGameHours();
            if (deltaGameHours <= 0f)
            {
                MaybeReportProfiler();
                return;
            }

            var activeProjects = GetActiveProjects();
            if (activeProjects.Count == 0)
            {
                _nextBuildProjectIndex = 0;
                MaybeReportProfiler();
                return;
            }

            AccumulateWorkerProgress(activeProjects, deltaGameHours);
            BuildReadyPiecesWithinFrameBudget(activeProjects);
            MaybeReportProfiler();
        }
    }

    private float ConsumeDeltaGameHours()
    {
        using (WyrdrasilProfiler.Sample("Construction.Progress.ConsumeDeltaGameHours"))
        {
            return _constructionGameTimeService.ConsumeDeltaGameHours();
        }
    }

    private IReadOnlyList<ConstructionProjectData> GetActiveProjects()
    {
        using (WyrdrasilProfiler.Sample("Construction.Progress.GetActiveProjects"))
        {
            return _constructionProjectService.Projects
                .Where(project => project.State != ConstructionProjectState.Completed && project.State != ConstructionProjectState.Blocked)
                .OrderBy(project => project.Id)
                .ToList();
        }
    }

    private void AccumulateWorkerProgress(IEnumerable<ConstructionProjectData> activeProjects, float deltaGameHours)
    {
        using (WyrdrasilProfiler.Sample("Construction.Progress.AccumulateWorkerProgress"))
        {
            foreach (var project in activeProjects)
            {
                using (WyrdrasilProfiler.Sample("Construction.Progress.GetActiveWorkerCount"))
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
        }
    }

    private void BuildReadyPiecesWithinFrameBudget(IReadOnlyList<ConstructionProjectData> activeProjects)
    {
        using (WyrdrasilProfiler.Sample("Construction.Progress.BuildReadyPiecesWithinFrameBudget"))
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

                using (WyrdrasilProfiler.Sample("Construction.Progress.TryConsumeOnePieceWork"))
                {
                    if (!_constructionProjectService.TryConsumeOnePieceWork(project.Id, out var remainingAccumulatedWork))
                    {
                        continue;
                    }

                    builtPieceCountThisUpdate++;
                    _nextBuildProjectIndex = (projectIndex + 1) % activeProjects.Count;

                    if (!TryBuildNextPiece(project, remainingAccumulatedWork))
                    {
                        continue;
                    }
                }
            }
        }
    }

    private bool TryBuildNextPiece(ConstructionProjectData project, float remainingAccumulatedWork)
    {
        using (WyrdrasilProfiler.Sample("Construction.Progress.TryBuildNextPieceDispatch"))
        {
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
                return false;
            }

            _debugLogService.Verbose(
                "Progress",
                $"Construction project {project.Id} built piece {pieceId}. Progress: {builtPieceCount}/{project.Progress.TotalPieceCount}. Remaining accumulated work: {remainingAccumulatedWork:0.##}.");

            if (isCompleted)
            {
                _debugLogService.Info("Progress", $"Construction project {project.Id} is now completed.");
            }

            return true;
        }
    }

    private void MaybeReportProfiler()
    {
        if (_profilerReportTimer.Elapsed.TotalSeconds < ProfilerReportIntervalSeconds)
        {
            return;
        }

        _profilerReportTimer.Restart();
        var report = WyrdrasilProfiler.TakeReportAndReset(ProfilerScope);
        if (!report.HasEntries)
        {
            return;
        }

        _debugLogService.Info(
            "Profiler",
            $"Last {report.WindowSeconds:0.0}s, top construction costs by max frame time:\n{report.ToMultilineString()}");
    }
}
