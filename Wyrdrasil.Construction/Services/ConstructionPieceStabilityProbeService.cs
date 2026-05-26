using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Wyrdrasil.Construction.Models;

namespace Wyrdrasil.Construction.Services;

/// <summary>
/// Central oracle used by construction projects to decide whether a blueprint piece is safe to build now.
/// This first implementation deliberately stays conservative and native-aware: it uses the real world scene
/// around the candidate instead of trusting the blueprint order, while keeping the API isolated so a deeper
/// WearNTear/hammer-ghost probe can replace this implementation later without changing project logic.
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

        return EvaluateCandidate(project, blueprint, piece, placement, builtPieceIds, lowestBlueprintY);
    }

    public ConstructionStabilityProbeResult EvaluateCandidate(
        ConstructionProjectData project,
        StructureBlueprintData blueprint,
        BlueprintPieceData piece,
        ConstructionResolvedPiecePlacement placement,
        ISet<int> builtPieceIds,
        float lowestBlueprintY)
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
                Reason = "Candidate is connected to an already built dependency."
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
            Reason = "Candidate has no grounded support and no built dependency yet."
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
