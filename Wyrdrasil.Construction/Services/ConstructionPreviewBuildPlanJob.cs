using System.Collections.Generic;
using System.Linq;
using Wyrdrasil.Construction.Models;

namespace Wyrdrasil.Construction.Services;

/// <summary>
/// Incremental, non-mutating preview build-order simulation.
///
/// The job processes a limited number of candidate stability probes per tick so that preview
/// analysis can be spread across several frames. This keeps the placement ghost responsive today
/// and gives the future native WearNTear probe a safe execution boundary.
/// </summary>
public sealed class ConstructionPreviewBuildPlanJob
{
    private const float FrontierContactRadius = 3.25f;

    private readonly ConstructionPieceStabilityProbeService _stabilityProbeService;
    private readonly StructureBlueprintData _blueprint;
    private readonly IReadOnlyDictionary<int, ConstructionResolvedPiecePlacement> _placementsByPieceId;
    private readonly Dictionary<int, ConstructionPreviewPieceEvaluation> _evaluationsByPieceId;
    private readonly HashSet<int> _remainingPieceIds;
    private readonly HashSet<int> _simulatedBuiltPieceIds = new();
    private readonly ConstructionProjectData _syntheticProject = new();
    private readonly int _maxWaves;

    private List<BlueprintPieceData> _currentWaveCandidates = new();
    private List<int> _buildableThisWave = new();
    private int _currentWaveCandidateIndex;
    private int _waveIndex;
    private int _safetyWaveCounter;

    public ConstructionPreviewBuildPlanJob(
        ConstructionPieceStabilityProbeService stabilityProbeService,
        StructureBlueprintData blueprint,
        IReadOnlyList<ConstructionResolvedPiecePlacement> placements)
    {
        _stabilityProbeService = stabilityProbeService;
        _blueprint = blueprint;
        _placementsByPieceId = placements.ToDictionary(placement => placement.PieceId);
        _evaluationsByPieceId = blueprint.Pieces.ToDictionary(
            piece => piece.PieceId,
            piece => new ConstructionPreviewPieceEvaluation
            {
                PieceId = piece.PieceId,
                State = ConstructionPieceBuildState.Pending,
                StabilityLevel = ConstructionPieceStabilityLevel.Unknown,
                BuildWave = -1,
                Reason = "Waiting for preview stability analysis."
            });
        _remainingPieceIds = new HashSet<int>(blueprint.Pieces.Select(piece => piece.PieceId));
        _maxWaves = blueprint.Pieces.Count + 1;

        if (blueprint.Pieces.Count == 0 || placements.Count == 0)
        {
            IsComplete = true;
        }
    }

    public bool IsComplete { get; private set; }

    public IReadOnlyDictionary<int, ConstructionPreviewPieceEvaluation> CurrentEvaluations => _evaluationsByPieceId;

    public void Tick(int maxCandidateProbes)
    {
        if (IsComplete)
        {
            return;
        }

        var remainingBudget = maxCandidateProbes <= 0 ? 1 : maxCandidateProbes;
        while (remainingBudget > 0 && !IsComplete)
        {
            if (_remainingPieceIds.Count == 0)
            {
                IsComplete = true;
                return;
            }

            if (_safetyWaveCounter >= _maxWaves)
            {
                MarkRemainingAsBlocked(
                    ConstructionPieceStabilityLevel.Unsupported,
                    "Preview build-order simulation reached its safety limit before resolving this piece.");
                IsComplete = true;
                return;
            }

            if (_currentWaveCandidates.Count == 0)
            {
                BeginNextWave();
                if (IsComplete)
                {
                    return;
                }
            }

            while (remainingBudget > 0 && _currentWaveCandidateIndex < _currentWaveCandidates.Count)
            {
                EvaluateCandidate(_currentWaveCandidates[_currentWaveCandidateIndex]);
                _currentWaveCandidateIndex++;
                remainingBudget--;
            }

            if (_currentWaveCandidateIndex >= _currentWaveCandidates.Count)
            {
                CompleteCurrentWave();
            }
        }
    }

    private void BeginNextWave()
    {
        _currentWaveCandidates = _blueprint.Pieces
            .Where(piece => _remainingPieceIds.Contains(piece.PieceId))
            .Where(piece => _placementsByPieceId.ContainsKey(piece.PieceId))
            .Where(piece => IsOnConstructionFrontier(piece, _placementsByPieceId[piece.PieceId]))
            .OrderBy(piece => piece.BuildOrder)
            .ThenBy(piece => piece.LocalPosition.y)
            .ThenBy(piece => piece.PieceId)
            .ToList();

        _currentWaveCandidateIndex = 0;
        _buildableThisWave = new List<int>();
        _safetyWaveCounter++;

        if (_currentWaveCandidates.Count > 0)
        {
            return;
        }

        MarkRemainingAsBlocked(
            ConstructionPieceStabilityLevel.Unsupported,
            "No connection to the current simulated construction frontier.");
        IsComplete = true;
    }

    private void EvaluateCandidate(BlueprintPieceData piece)
    {
        var placement = _placementsByPieceId[piece.PieceId];
        var probeResult = _stabilityProbeService.EvaluateCandidate(
            _syntheticProject,
            _blueprint,
            piece,
            placement,
            _simulatedBuiltPieceIds);

        var evaluation = _evaluationsByPieceId[piece.PieceId];
        evaluation.State = probeResult.IsBuildable
            ? ConstructionPieceBuildState.Buildable
            : ConstructionPieceBuildState.Blocked;
        evaluation.StabilityLevel = probeResult.Level;
        evaluation.BuildWave = probeResult.IsBuildable ? _waveIndex : -1;
        evaluation.Reason = probeResult.Reason;

        if (probeResult.IsBuildable)
        {
            _buildableThisWave.Add(piece.PieceId);
        }
    }

    private void CompleteCurrentWave()
    {
        if (_buildableThisWave.Count == 0)
        {
            foreach (var pieceId in _remainingPieceIds.ToList())
            {
                if (_evaluationsByPieceId[pieceId].State == ConstructionPieceBuildState.Blocked)
                {
                    continue;
                }

                _evaluationsByPieceId[pieceId].State = ConstructionPieceBuildState.Blocked;
                _evaluationsByPieceId[pieceId].StabilityLevel = ConstructionPieceStabilityLevel.Unsupported;
                _evaluationsByPieceId[pieceId].BuildWave = -1;
                _evaluationsByPieceId[pieceId].Reason = "The simulated construction frontier cannot safely expand to this piece yet.";
            }

            IsComplete = true;
            return;
        }

        foreach (var pieceId in _buildableThisWave)
        {
            _simulatedBuiltPieceIds.Add(pieceId);
            _remainingPieceIds.Remove(pieceId);
        }

        _waveIndex++;
        _currentWaveCandidates.Clear();
        _currentWaveCandidateIndex = 0;
        _buildableThisWave.Clear();
    }

    private bool IsOnConstructionFrontier(BlueprintPieceData piece, ConstructionResolvedPiecePlacement placement)
    {
        if (_simulatedBuiltPieceIds.Count == 0)
        {
            var lowestY = _blueprint.Pieces.Count == 0
                ? piece.LocalPosition.y
                : _blueprint.Pieces.Min(candidate => candidate.LocalPosition.y);
            return _stabilityProbeService.IsGroundRoot(piece, placement, lowestY);
        }

        if (piece.DependencyPieceIds.Any(_simulatedBuiltPieceIds.Contains))
        {
            return true;
        }

        foreach (var builtPieceId in _simulatedBuiltPieceIds)
        {
            if (!_placementsByPieceId.TryGetValue(builtPieceId, out var builtPlacement))
            {
                continue;
            }

            if ((builtPlacement.WorldPosition - placement.WorldPosition).sqrMagnitude <= FrontierContactRadius * FrontierContactRadius)
            {
                return true;
            }
        }

        return false;
    }

    private void MarkRemainingAsBlocked(ConstructionPieceStabilityLevel stabilityLevel, string reason)
    {
        foreach (var pieceId in _remainingPieceIds)
        {
            var evaluation = _evaluationsByPieceId[pieceId];
            evaluation.State = ConstructionPieceBuildState.Blocked;
            evaluation.StabilityLevel = stabilityLevel;
            evaluation.BuildWave = -1;
            evaluation.Reason = reason;
        }
    }
}
