using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Wyrdrasil.Construction.Models;

namespace Wyrdrasil.Construction.Services;

public sealed class ConstructionPieceBuildOrderService
{
    private const float FrontierContactRadius = 3.25f;
    private const float FrontierContactRadiusSquared = FrontierContactRadius * FrontierContactRadius;

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
        var placementsByPieceId = new Dictionary<int, ConstructionResolvedPiecePlacement>();
        foreach (var placement in placements)
        {
            placementsByPieceId[placement.PieceId] = placement;
        }

        var lowestLocalY = 0f;
        if (blueprint.Pieces.Count > 0)
        {
            lowestLocalY = blueprint.Pieces.Min(candidate => candidate.LocalPosition.y);
        }

        return TrySelectNextBuildablePiece(
            project,
            blueprint,
            placements,
            placementsByPieceId,
            lowestLocalY,
            out selectedPlacement,
            out selectedProbeResult,
            out failureReason);
    }

    public bool TrySelectNextBuildablePiece(
        ConstructionProjectData project,
        StructureBlueprintData blueprint,
        IReadOnlyList<ConstructionResolvedPiecePlacement> placements,
        IReadOnlyDictionary<int, ConstructionResolvedPiecePlacement> placementsByPieceId,
        float lowestLocalY,
        out ConstructionResolvedPiecePlacement selectedPlacement,
        out ConstructionStabilityProbeResult selectedProbeResult,
        out string failureReason)
    {
        selectedPlacement = new ConstructionResolvedPiecePlacement();
        selectedProbeResult = new ConstructionStabilityProbeResult();

        _constructionProjectService.EnsurePieceProgress(project, blueprint);

        var progressByPieceId = new Dictionary<int, ConstructionPieceProgressData>();
        foreach (var progress in project.Progress.Pieces)
        {
            progressByPieceId[progress.PieceId] = progress;
        }

        var builtPieceIds = new HashSet<int>();
        foreach (var progress in project.Progress.Pieces)
        {
            if (progress.State == ConstructionPieceBuildState.Built)
            {
                builtPieceIds.Add(progress.PieceId);
            }
        }

        if (builtPieceIds.Count >= blueprint.Pieces.Count)
        {
            failureReason = $"Construction project {project.Id} has no pending pieces.";
            return false;
        }

        var orderedPieces = blueprint.Pieces
            .OrderBy(candidate => candidate.BuildOrder)
            .ThenBy(candidate => candidate.PieceId)
            .ToList();
        var builtFrontierIndex = BuiltFrontierIndex.Create(placementsByPieceId, builtPieceIds);
        BuildableCandidate? selected = null;

        foreach (var piece in orderedPieces)
        {
            if (!placementsByPieceId.TryGetValue(piece.PieceId, out var placement))
            {
                continue;
            }

            if (!progressByPieceId.TryGetValue(piece.PieceId, out var progress))
            {
                progress = _constructionProjectService.GetOrCreatePieceProgress(project, piece.PieceId);
                progressByPieceId[piece.PieceId] = progress;
            }

            if (progress.State == ConstructionPieceBuildState.Built || progress.State == ConstructionPieceBuildState.Reserved)
            {
                continue;
            }

            if (!IsOnConstructionFrontier(piece, placement, lowestLocalY, builtPieceIds, builtFrontierIndex))
            {
                SetPieceStateIfChanged(
                    project,
                    progress,
                    ConstructionPieceBuildState.Pending,
                    ConstructionPieceStabilityLevel.Unknown);
                continue;
            }

            var probeResult = _stabilityProbeService.EvaluateCandidate(project, blueprint, piece, placement, builtPieceIds, lowestLocalY);
            SetPieceStateIfChanged(
                project,
                progress,
                probeResult.IsBuildable ? ConstructionPieceBuildState.Buildable : ConstructionPieceBuildState.Blocked,
                probeResult.Level);

            if (!probeResult.IsBuildable)
            {
                continue;
            }

            var candidate = new BuildableCandidate(piece, placement, probeResult);
            if (selected == null || IsBetterCandidate(candidate, selected))
            {
                selected = candidate;
            }
        }

        if (selected == null)
        {
            var pendingCount = 0;
            foreach (var progress in project.Progress.Pieces)
            {
                if (progress.State != ConstructionPieceBuildState.Built)
                {
                    pendingCount++;
                }
            }

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

    private void SetPieceStateIfChanged(
        ConstructionProjectData project,
        ConstructionPieceProgressData progress,
        ConstructionPieceBuildState state,
        ConstructionPieceStabilityLevel stabilityLevel)
    {
        if (progress.State == state && progress.LastStabilityLevel == stabilityLevel)
        {
            return;
        }

        _constructionProjectService.TrySetPieceBuildState(project.Id, progress.PieceId, state, stabilityLevel);
    }

    private bool IsOnConstructionFrontier(
        BlueprintPieceData piece,
        ConstructionResolvedPiecePlacement placement,
        float lowestLocalY,
        ISet<int> builtPieceIds,
        BuiltFrontierIndex builtFrontierIndex)
    {
        if (builtPieceIds.Count == 0)
        {
            return _stabilityProbeService.IsGroundRoot(piece, placement, lowestLocalY);
        }

        if (piece.DependencyPieceIds.Any(builtPieceIds.Contains))
        {
            return true;
        }

        return builtFrontierIndex.HasBuiltPieceWithinRadius(placement.WorldPosition, FrontierContactRadiusSquared);
    }

    private static bool IsBetterCandidate(BuildableCandidate candidate, BuildableCandidate selected)
    {
        var candidateRank = GetStabilityRank(candidate.ProbeResult.Level);
        var selectedRank = GetStabilityRank(selected.ProbeResult.Level);
        if (candidateRank != selectedRank)
        {
            return candidateRank > selectedRank;
        }

        if (candidate.Piece.BuildOrder != selected.Piece.BuildOrder)
        {
            return candidate.Piece.BuildOrder < selected.Piece.BuildOrder;
        }

        if (!Mathf.Approximately(candidate.Piece.LocalPosition.y, selected.Piece.LocalPosition.y))
        {
            return candidate.Piece.LocalPosition.y < selected.Piece.LocalPosition.y;
        }

        return candidate.Piece.PieceId < selected.Piece.PieceId;
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

    private sealed class BuiltFrontierIndex
    {
        private readonly Dictionary<GridKey, List<Vector3>> _positionsByCell;

        private BuiltFrontierIndex(Dictionary<GridKey, List<Vector3>> positionsByCell)
        {
            _positionsByCell = positionsByCell;
        }

        public static BuiltFrontierIndex Create(
            IReadOnlyDictionary<int, ConstructionResolvedPiecePlacement> placementsByPieceId,
            IEnumerable<int> builtPieceIds)
        {
            var positionsByCell = new Dictionary<GridKey, List<Vector3>>();
            foreach (var builtPieceId in builtPieceIds)
            {
                if (!placementsByPieceId.TryGetValue(builtPieceId, out var placement))
                {
                    continue;
                }

                var key = GridKey.FromPosition(placement.WorldPosition, FrontierContactRadius);
                if (!positionsByCell.TryGetValue(key, out var positions))
                {
                    positions = new List<Vector3>();
                    positionsByCell[key] = positions;
                }

                positions.Add(placement.WorldPosition);
            }

            return new BuiltFrontierIndex(positionsByCell);
        }

        public bool HasBuiltPieceWithinRadius(Vector3 position, float radiusSquared)
        {
            if (_positionsByCell.Count == 0)
            {
                return false;
            }

            var center = GridKey.FromPosition(position, FrontierContactRadius);
            for (var x = center.X - 1; x <= center.X + 1; x++)
            {
                for (var y = center.Y - 1; y <= center.Y + 1; y++)
                {
                    for (var z = center.Z - 1; z <= center.Z + 1; z++)
                    {
                        if (!_positionsByCell.TryGetValue(new GridKey(x, y, z), out var positions))
                        {
                            continue;
                        }

                        foreach (var builtPosition in positions)
                        {
                            if ((builtPosition - position).sqrMagnitude <= radiusSquared)
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }
    }

    private readonly struct GridKey : IEquatable<GridKey>
    {
        public GridKey(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public int X { get; }
        public int Y { get; }
        public int Z { get; }

        public static GridKey FromPosition(Vector3 position, float cellSize)
        {
            return new GridKey(
                Mathf.FloorToInt(position.x / cellSize),
                Mathf.FloorToInt(position.y / cellSize),
                Mathf.FloorToInt(position.z / cellSize));
        }

        public bool Equals(GridKey other) => X == other.X && Y == other.Y && Z == other.Z;

        public override bool Equals(object? obj) => obj is GridKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = X;
                hashCode = (hashCode * 397) ^ Y;
                hashCode = (hashCode * 397) ^ Z;
                return hashCode;
            }
        }
    }
}
