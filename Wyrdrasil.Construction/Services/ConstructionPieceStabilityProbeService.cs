using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Wyrdrasil.Construction.Models;

namespace Wyrdrasil.Construction.Services;

/// <summary>
/// Central oracle used by construction projects to decide whether a blueprint piece is safe to build now.
/// It remains conservative, but now accepts a native world-piece graph signal so manual player supports and
/// already-present Valheim build pieces can participate in construction order without falling back to global
/// object-name searches.
/// </summary>
public sealed class ConstructionPieceStabilityProbeService
{
    private const float GroundRayStartHeight = 0.75f;
    private const float GroundRayDistance = 1.35f;

    public ConstructionStabilityProbeResult EvaluateCandidate(
        ConstructionProjectData project,
        StructureBlueprintData blueprint,
        BlueprintPieceData piece,
        ConstructionResolvedPiecePlacement placement,
        ISet<int> builtPieceIds)
    {
        var lowestBlueprintY = blueprint.Pieces.Count == 0
            ? piece.LocalPosition.y
            : blueprint.Pieces.Min(candidate => candidate.LocalPosition.y);

        return EvaluateCandidate(project, blueprint, piece, placement, builtPieceIds, lowestBlueprintY, false);
    }

    public ConstructionStabilityProbeResult EvaluateCandidate(
        ConstructionProjectData project,
        StructureBlueprintData blueprint,
        BlueprintPieceData piece,
        ConstructionResolvedPiecePlacement placement,
        ISet<int> builtPieceIds,
        float lowestBlueprintY)
    {
        return EvaluateCandidate(project, blueprint, piece, placement, builtPieceIds, lowestBlueprintY, false);
    }

    public ConstructionStabilityProbeResult EvaluateCandidate(
        ConstructionProjectData project,
        StructureBlueprintData blueprint,
        BlueprintPieceData piece,
        ConstructionResolvedPiecePlacement placement,
        ISet<int> builtPieceIds,
        float lowestBlueprintY,
        bool hasNativeWorldSupport)
    {
        if (IsGroundRoot(piece, placement, lowestBlueprintY))
        {
            return new ConstructionStabilityProbeResult
            {
                Level = ConstructionPieceStabilityLevel.Grounded,
                IsBuildable = true,
                Reason = "Candidate is grounded or belongs to the first structural layer."
            };
        }

        if (piece.DependencyPieceIds.Any(builtPieceIds.Contains))
        {
            return new ConstructionStabilityProbeResult
            {
                Level = ConstructionPieceStabilityLevel.Strong,
                IsBuildable = true,
                Reason = "Candidate is connected to an already built blueprint dependency."
            };
        }

        if (hasNativeWorldSupport)
        {
            return new ConstructionStabilityProbeResult
            {
                Level = ConstructionPieceStabilityLevel.Acceptable,
                IsBuildable = true,
                Reason = "Candidate is connected to the native Valheim build-piece graph near the project."
            };
        }

        if (builtPieceIds.Count > 0)
        {
            return new ConstructionStabilityProbeResult
            {
                Level = ConstructionPieceStabilityLevel.Acceptable,
                IsBuildable = true,
                Reason = "Candidate is on the active construction frontier."
            };
        }

        return new ConstructionStabilityProbeResult
        {
            Level = ConstructionPieceStabilityLevel.Unsupported,
            IsBuildable = false,
            Reason = "Candidate has no grounded support, no built dependency and no native world support yet."
        };
    }

    public bool IsGroundRoot(BlueprintPieceData piece, ConstructionResolvedPiecePlacement placement, float lowestBlueprintY)
    {
        if (piece.LocalPosition.y <= lowestBlueprintY + 0.35f)
        {
            return true;
        }

        var rayOrigin = placement.WorldPosition + Vector3.up * GroundRayStartHeight;
        if (!Physics.Raycast(rayOrigin, Vector3.down, out var hit, GroundRayDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        return hit.collider != null && hit.collider.GetComponentInParent<Piece>() == null;
    }
}
