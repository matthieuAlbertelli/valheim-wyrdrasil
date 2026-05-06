using System.Collections.Generic;
using System.Linq;
using Wyrdrasil.Construction.Models;

namespace Wyrdrasil.Construction.Services;

/// <summary>
/// Computes a non-mutating buildability simulation for blueprint placement preview.
/// It mirrors the construction frontier idea used at runtime, but does not create or mutate a
/// ConstructionProjectData. The result is purely visual: roots first, then pieces unlocked by the
/// simulated built frontier, with blocked pieces left visible as diagnostics.
/// </summary>
public sealed class ConstructionPreviewBuildPlanService
{
    private const float FrontierContactRadius = 3.25f;

    private readonly ConstructionPieceStabilityProbeService _stabilityProbeService;

    public ConstructionPreviewBuildPlanService(ConstructionPieceStabilityProbeService stabilityProbeService)
    {
        _stabilityProbeService = stabilityProbeService;
    }

    public IReadOnlyDictionary<int, ConstructionPreviewPieceEvaluation> EvaluatePreview(
        StructureBlueprintData blueprint,
        IReadOnlyList<ConstructionResolvedPiecePlacement> placements)
    {
        var evaluationsByPieceId = blueprint.Pieces.ToDictionary(
            piece => piece.PieceId,
            piece => new ConstructionPreviewPieceEvaluation
            {
                PieceId = piece.PieceId,
                State = ConstructionPieceBuildState.Pending,
                StabilityLevel = ConstructionPieceStabilityLevel.Unknown,
                BuildWave = -1,
                Reason = "Not evaluated yet."
            });

        if (blueprint.Pieces.Count == 0 || placements.Count == 0)
        {
            return evaluationsByPieceId;
        }

        var placementsByPieceId = placements.ToDictionary(placement => placement.PieceId);
        var remainingPieceIds = new HashSet<int>(blueprint.Pieces.Select(piece => piece.PieceId));
        var simulatedBuiltPieceIds = new HashSet<int>();
        var syntheticProject = new ConstructionProjectData();
        var waveIndex = 0;
        var maxWaves = blueprint.Pieces.Count + 1;

        for (var safety = 0; safety < maxWaves && remainingPieceIds.Count > 0; safety++)
        {
            var waveCandidates = blueprint.Pieces
                .Where(piece => remainingPieceIds.Contains(piece.PieceId))
                .Where(piece => placementsByPieceId.ContainsKey(piece.PieceId))
                .Where(piece => IsOnConstructionFrontier(piece, placementsByPieceId[piece.PieceId], blueprint, placementsByPieceId, simulatedBuiltPieceIds))
                .OrderBy(piece => piece.BuildOrder)
                .ThenBy(piece => piece.LocalPosition.y)
                .ThenBy(piece => piece.PieceId)
                .ToList();

            if (waveCandidates.Count == 0)
            {
                MarkRemainingAsBlocked(
                    remainingPieceIds,
                    evaluationsByPieceId,
                    ConstructionPieceStabilityLevel.Unsupported,
                    "No connection to the current simulated construction frontier.");
                break;
            }

            var buildableThisWave = new List<int>();
            foreach (var piece in waveCandidates)
            {
                var placement = placementsByPieceId[piece.PieceId];
                var probeResult = _stabilityProbeService.EvaluateCandidate(
                    syntheticProject,
                    blueprint,
                    piece,
                    placement,
                    simulatedBuiltPieceIds);

                var evaluation = evaluationsByPieceId[piece.PieceId];
                evaluation.State = probeResult.IsBuildable
                    ? ConstructionPieceBuildState.Buildable
                    : ConstructionPieceBuildState.Blocked;
                evaluation.StabilityLevel = probeResult.Level;
                evaluation.BuildWave = probeResult.IsBuildable ? waveIndex : -1;
                evaluation.Reason = probeResult.Reason;

                if (probeResult.IsBuildable)
                {
                    buildableThisWave.Add(piece.PieceId);
                }
            }

            if (buildableThisWave.Count == 0)
            {
                foreach (var pieceId in remainingPieceIds.ToList())
                {
                    if (evaluationsByPieceId[pieceId].State != ConstructionPieceBuildState.Blocked)
                    {
                        evaluationsByPieceId[pieceId].State = ConstructionPieceBuildState.Blocked;
                        evaluationsByPieceId[pieceId].StabilityLevel = ConstructionPieceStabilityLevel.Unsupported;
                        evaluationsByPieceId[pieceId].Reason = "The simulated construction frontier cannot safely expand to this piece yet.";
                    }
                }

                break;
            }

            foreach (var pieceId in buildableThisWave)
            {
                simulatedBuiltPieceIds.Add(pieceId);
                remainingPieceIds.Remove(pieceId);
            }

            waveIndex++;
        }

        if (remainingPieceIds.Count > 0)
        {
            MarkRemainingAsBlocked(
                remainingPieceIds,
                evaluationsByPieceId,
                ConstructionPieceStabilityLevel.Unsupported,
                "Preview build-order simulation reached its safety limit before resolving this piece.");
        }

        return evaluationsByPieceId;
    }

    private bool IsOnConstructionFrontier(
        BlueprintPieceData piece,
        ConstructionResolvedPiecePlacement placement,
        StructureBlueprintData blueprint,
        IReadOnlyDictionary<int, ConstructionResolvedPiecePlacement> placementsByPieceId,
        ISet<int> builtPieceIds)
    {
        if (builtPieceIds.Count == 0)
        {
            var lowestY = blueprint.Pieces.Count == 0
                ? piece.LocalPosition.y
                : blueprint.Pieces.Min(candidate => candidate.LocalPosition.y);
            return _stabilityProbeService.IsGroundRoot(piece, placement, lowestY);
        }

        if (piece.DependencyPieceIds.Any(builtPieceIds.Contains))
        {
            return true;
        }

        foreach (var builtPieceId in builtPieceIds)
        {
            if (!placementsByPieceId.TryGetValue(builtPieceId, out var builtPlacement))
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

    private static void MarkRemainingAsBlocked(
        IEnumerable<int> remainingPieceIds,
        IReadOnlyDictionary<int, ConstructionPreviewPieceEvaluation> evaluationsByPieceId,
        ConstructionPieceStabilityLevel stabilityLevel,
        string reason)
    {
        foreach (var pieceId in remainingPieceIds)
        {
            var evaluation = evaluationsByPieceId[pieceId];
            evaluation.State = ConstructionPieceBuildState.Blocked;
            evaluation.StabilityLevel = stabilityLevel;
            evaluation.BuildWave = -1;
            evaluation.Reason = reason;
        }
    }
}