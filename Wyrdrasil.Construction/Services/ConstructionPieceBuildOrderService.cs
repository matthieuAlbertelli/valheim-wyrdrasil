using System.Collections.Generic;
using System.Linq;
using Wyrdrasil.Construction.Models;

namespace Wyrdrasil.Construction.Services;

public sealed class ConstructionPieceBuildOrderService
{
    private const float FrontierContactRadius = 3.25f;

    private readonly ConstructionPieceStabilityProbeService _stabilityProbeService;
    private readonly ConstructionProjectService _constructionProjectService;

    public ConstructionPieceBuildOrderService(
        ConstructionPieceStabilityProbeService stabilityProbeService,
        ConstructionProjectService constructionProjectService)
    {
        _stabilityProbeService = stabilityProbeService;
        _constructionProjectService = constructionProjectService;
    }

    public bool TrySelectNextBuildablePiece(
        ConstructionProjectData project,
        StructureBlueprintData blueprint,
        IReadOnlyList<ConstructionResolvedPiecePlacement> placements,
        out ConstructionResolvedPiecePlacement selectedPlacement,
        out ConstructionStabilityProbeResult selectedProbeResult,
        out string failureReason)
    {
        selectedPlacement = new ConstructionResolvedPiecePlacement();
        selectedProbeResult = new ConstructionStabilityProbeResult();

        _constructionProjectService.EnsurePieceProgress(project, blueprint);

        var builtPieceIds = new HashSet<int>(project.Progress.Pieces
            .Where(progress => progress.State == ConstructionPieceBuildState.Built)
            .Select(progress => progress.PieceId));

        if (builtPieceIds.Count >= blueprint.Pieces.Count)
        {
            failureReason = $"Construction project {project.Id} has no pending pieces.";
            return false;
        }

        var placementsByPieceId = placements.ToDictionary(placement => placement.PieceId);
        var candidates = new List<BuildableCandidate>();

        foreach (var piece in blueprint.Pieces.OrderBy(candidate => candidate.BuildOrder).ThenBy(candidate => candidate.PieceId))
        {
            if (!placementsByPieceId.TryGetValue(piece.PieceId, out var placement))
            {
                continue;
            }

            var progress = _constructionProjectService.GetOrCreatePieceProgress(project, piece.PieceId);
            if (progress.State == ConstructionPieceBuildState.Built || progress.State == ConstructionPieceBuildState.Reserved)
            {
                continue;
            }

            if (!IsOnConstructionFrontier(piece, placement, blueprint, placementsByPieceId, builtPieceIds))
            {
                _constructionProjectService.TrySetPieceBuildState(
                    project.Id,
                    piece.PieceId,
                    ConstructionPieceBuildState.Blocked,
                    ConstructionPieceStabilityLevel.Unsupported);
                continue;
            }

            var probeResult = _stabilityProbeService.EvaluateCandidate(project, blueprint, piece, placement, builtPieceIds);
            _constructionProjectService.TrySetPieceBuildState(
                project.Id,
                piece.PieceId,
                probeResult.IsBuildable ? ConstructionPieceBuildState.Buildable : ConstructionPieceBuildState.Blocked,
                probeResult.Level);

            if (!probeResult.IsBuildable)
            {
                continue;
            }

            candidates.Add(new BuildableCandidate(piece, placement, probeResult));
        }

        var selected = candidates
            .OrderByDescending(candidate => GetStabilityRank(candidate.ProbeResult.Level))
            .ThenBy(candidate => candidate.Piece.BuildOrder)
            .ThenBy(candidate => candidate.Piece.LocalPosition.y)
            .ThenBy(candidate => candidate.Piece.PieceId)
            .FirstOrDefault();

        if (selected == null)
        {
            var pendingCount = project.Progress.Pieces.Count(progress => progress.State != ConstructionPieceBuildState.Built);
            failureReason = pendingCount <= 0
                ? $"Construction project {project.Id} is complete."
                : $"Construction project {project.Id} has {pendingCount} pending piece(s), but none are safe to build from the current structural frontier.";
            return false;
        }

        if (project.State == ConstructionProjectState.Blocked)
        {
            _constructionProjectService.TrySetState(
                project.Id,
                project.Progress.BuiltPieceCount > 0 ? ConstructionProjectState.InProgress : ConstructionProjectState.ReadyForWork);
        }

        selectedPlacement = selected.Placement;
        selectedProbeResult = selected.ProbeResult;
        failureReason = string.Empty;
        return true;
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

    private static int GetStabilityRank(ConstructionPieceStabilityLevel level)
    {
        return level switch
        {
            ConstructionPieceStabilityLevel.Grounded => 5,
            ConstructionPieceStabilityLevel.Strong => 4,
            ConstructionPieceStabilityLevel.Acceptable => 3,
            ConstructionPieceStabilityLevel.Weak => 2,
            ConstructionPieceStabilityLevel.Unsupported => 1,
            _ => 0
        };
    }

    private sealed class BuildableCandidate
    {
        public BlueprintPieceData Piece { get; }
        public ConstructionResolvedPiecePlacement Placement { get; }
        public ConstructionStabilityProbeResult ProbeResult { get; }

        public BuildableCandidate(
            BlueprintPieceData piece,
            ConstructionResolvedPiecePlacement placement,
            ConstructionStabilityProbeResult probeResult)
        {
            Piece = piece;
            Placement = placement;
            ProbeResult = probeResult;
        }
    }
}
