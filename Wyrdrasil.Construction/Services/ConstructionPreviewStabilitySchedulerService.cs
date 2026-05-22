using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Wyrdrasil.Construction.Models;

namespace Wyrdrasil.Construction.Services;

/// <summary>
/// Schedules construction preview stability analysis without blocking ghost movement.
///
/// The placement preview may move every frame; stability probing should not. This service debounces
/// movement, quantizes the preview transform for cache reuse, and processes build-plan analysis in
/// small batches. It is deliberately placed between the visual preview and the stability oracle so a
/// future native WearNTear probe can be swapped in without changing preview UX code.
/// </summary>
public sealed class ConstructionPreviewStabilitySchedulerService
{
    private const float PositionQuantizationMeters = 0.5f;
    private const float RotationQuantizationDegrees = 45f;
    private const float DebounceSeconds = 0.22f;
    private const int MaxCandidateProbesPerTick = 12;
    private const int MaxCachedResults = 24;

    private readonly ConstructionPreviewBuildPlanService _buildPlanService;
    private readonly Dictionary<string, Dictionary<int, ConstructionPreviewPieceEvaluation>> _cachedEvaluationsByKey = new();
    private readonly Queue<string> _cacheInsertionOrder = new();

    private string _pendingKey = string.Empty;
    private string _activeJobKey = string.Empty;
    private string _currentResultKey = string.Empty;
    private StructureBlueprintData? _pendingBlueprint;
    private List<ConstructionResolvedPiecePlacement> _pendingPlacements = new();
    private float _scheduledAnalysisTime;
    private bool _hasPendingAnalysis;
    private ConstructionPreviewBuildPlanJob? _activeJob;
    private IReadOnlyDictionary<int, ConstructionPreviewPieceEvaluation>? _currentEvaluations;

    public ConstructionPreviewStabilitySchedulerService(ConstructionPreviewBuildPlanService buildPlanService)
    {
        _buildPlanService = buildPlanService;
    }

    public string StatusLabel { get; private set; } = "Stability idle.";

    public void RequestAnalysis(
        StructureBlueprintData blueprint,
        IReadOnlyList<ConstructionResolvedPiecePlacement> placements,
        Vector3 originPosition,
        Quaternion originRotation,
        float now)
    {
        var key = CreateAnalysisKey(blueprint.Id, originPosition, originRotation);

        if (_activeJob != null && _activeJobKey == key)
        {
            StatusLabel = _activeJob.IsComplete ? "Stability ready." : "Stability analyzing...";
            return;
        }

        if (_currentResultKey == key && _currentEvaluations != null)
        {
            StatusLabel = "Stability ready.";
            return;
        }

        if (_cachedEvaluationsByKey.TryGetValue(key, out var cachedEvaluations))
        {
            CancelActiveJob();
            _hasPendingAnalysis = false;
            _pendingKey = string.Empty;
            _currentResultKey = key;
            _currentEvaluations = cachedEvaluations;
            StatusLabel = "Stability cached.";
            return;
        }

        if (_hasPendingAnalysis && _pendingKey == key)
        {
            _pendingBlueprint = blueprint;
            _pendingPlacements = placements.ToList();
            StatusLabel = "Stability waiting for stable placement...";
            return;
        }

        CancelActiveJob();
        _pendingKey = key;
        _pendingBlueprint = blueprint;
        _pendingPlacements = placements.ToList();
        _scheduledAnalysisTime = now + DebounceSeconds;
        _hasPendingAnalysis = true;
        _currentResultKey = string.Empty;
        _currentEvaluations = null;
        StatusLabel = "Stability waiting for stable placement...";
    }

    public void Tick(float now)
    {
        if (_hasPendingAnalysis && now >= _scheduledAnalysisTime)
        {
            BeginPendingAnalysis();
        }

        if (_activeJob == null)
        {
            return;
        }

        _activeJob.Tick(MaxCandidateProbesPerTick);
        _currentEvaluations = _activeJob.CurrentEvaluations;
        _currentResultKey = _activeJobKey;

        if (!_activeJob.IsComplete)
        {
            StatusLabel = "Stability analyzing...";
            return;
        }

        AddToCache(_activeJobKey, CloneEvaluations(_activeJob.CurrentEvaluations));
        StatusLabel = "Stability ready.";
        _activeJob = null;
        _activeJobKey = string.Empty;
    }

    public bool TryGetCurrentEvaluations(out IReadOnlyDictionary<int, ConstructionPreviewPieceEvaluation> evaluations)
    {
        if (_currentEvaluations == null)
        {
            evaluations = new Dictionary<int, ConstructionPreviewPieceEvaluation>();
            return false;
        }

        evaluations = _currentEvaluations;
        return true;
    }

    public void Reset()
    {
        CancelActiveJob();
        _pendingKey = string.Empty;
        _pendingBlueprint = null;
        _pendingPlacements.Clear();
        _hasPendingAnalysis = false;
        _scheduledAnalysisTime = 0f;
        _currentResultKey = string.Empty;
        _currentEvaluations = null;
        StatusLabel = "Stability idle.";
    }

    private void BeginPendingAnalysis()
    {
        if (_pendingBlueprint == null)
        {
            Reset();
            return;
        }

        _activeJob = _buildPlanService.CreateJob(_pendingBlueprint, _pendingPlacements);
        _activeJobKey = _pendingKey;
        _hasPendingAnalysis = false;
        _pendingKey = string.Empty;
        _pendingBlueprint = null;
        _pendingPlacements = new List<ConstructionResolvedPiecePlacement>();
        StatusLabel = "Stability analyzing...";
    }

    private void CancelActiveJob()
    {
        _activeJob = null;
        _activeJobKey = string.Empty;
    }

    private void AddToCache(string key, Dictionary<int, ConstructionPreviewPieceEvaluation> evaluations)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        if (!_cachedEvaluationsByKey.ContainsKey(key))
        {
            _cacheInsertionOrder.Enqueue(key);
        }

        _cachedEvaluationsByKey[key] = evaluations;

        while (_cacheInsertionOrder.Count > MaxCachedResults)
        {
            var staleKey = _cacheInsertionOrder.Dequeue();
            _cachedEvaluationsByKey.Remove(staleKey);
        }
    }

    private static Dictionary<int, ConstructionPreviewPieceEvaluation> CloneEvaluations(
        IReadOnlyDictionary<int, ConstructionPreviewPieceEvaluation> source)
    {
        return source.ToDictionary(
            pair => pair.Key,
            pair => new ConstructionPreviewPieceEvaluation
            {
                PieceId = pair.Value.PieceId,
                State = pair.Value.State,
                StabilityLevel = pair.Value.StabilityLevel,
                BuildWave = pair.Value.BuildWave,
                Reason = pair.Value.Reason
            });
    }

    private static string CreateAnalysisKey(string blueprintId, Vector3 originPosition, Quaternion originRotation)
    {
        var x = Quantize(originPosition.x, PositionQuantizationMeters);
        var y = Quantize(originPosition.y, PositionQuantizationMeters);
        var z = Quantize(originPosition.z, PositionQuantizationMeters);
        var yaw = Quantize(originRotation.eulerAngles.y, RotationQuantizationDegrees);
        return $"{blueprintId}|{x}|{y}|{z}|{yaw}";
    }

    private static int Quantize(float value, float step)
    {
        return Mathf.RoundToInt(value / step);
    }
}
