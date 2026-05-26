using System.Collections.Generic;
using UnityEngine;
using Wyrdrasil.Construction.Models;
using Wyrdrasil.Core.Diagnostics;

namespace Wyrdrasil.Construction.Services;

/// <summary>
/// Shared cache for world-space blueprint placements. Building, integrity and future diagnostics all need the
/// same projection from blueprint-local pieces to world-space targets; keeping it here prevents each subsystem
/// from resolving and sorting the whole blueprint independently.
/// </summary>
public sealed class ConstructionProjectPlacementCacheService
{
    private readonly ConstructionPlacementService _constructionPlacementService;
    private readonly Dictionary<int, CachedProjectPlacements> _placementCacheByProjectId = new();

    public ConstructionProjectPlacementCacheService(ConstructionPlacementService constructionPlacementService)
    {
        _constructionPlacementService = constructionPlacementService;
    }

    public void InvalidateProject(int projectId)
    {
        _placementCacheByProjectId.Remove(projectId);
    }

    public bool TryGetResolvedPlacements(
        ConstructionProjectData project,
        StructureBlueprintData blueprint,
        out CachedProjectPlacements cachedPlacements,
        out string failureReason)
    {
        using (WyrdrasilProfiler.Sample("Construction.Placements.Resolve"))
        {
            if (_placementCacheByProjectId.TryGetValue(project.Id, out cachedPlacements) &&
                cachedPlacements.Matches(project, blueprint))
            {
                failureReason = string.Empty;
                return true;
            }

            if (!_constructionPlacementService.TryResolveBlueprintPlacements(
                    blueprint,
                    project.OriginPosition,
                    project.OriginRotation,
                    out var placements,
                    out failureReason))
            {
                _placementCacheByProjectId.Remove(project.Id);
                cachedPlacements = CachedProjectPlacements.Empty;
                return false;
            }

            cachedPlacements = CachedProjectPlacements.Create(project, blueprint, placements);
            _placementCacheByProjectId[project.Id] = cachedPlacements;
            failureReason = string.Empty;
            return true;
        }
    }

    public sealed class CachedProjectPlacements
    {
        public static readonly CachedProjectPlacements Empty = new(
            0,
            string.Empty,
            0,
            Vector3.zero,
            Quaternion.identity,
            new List<ConstructionResolvedPiecePlacement>(),
            new Dictionary<int, ConstructionResolvedPiecePlacement>(),
            0f);

        private readonly int _projectId;
        private readonly string _blueprintId;
        private readonly int _blueprintPieceCount;
        private readonly Vector3 _originPosition;
        private readonly Quaternion _originRotation;

        private CachedProjectPlacements(
            int projectId,
            string blueprintId,
            int blueprintPieceCount,
            Vector3 originPosition,
            Quaternion originRotation,
            IReadOnlyList<ConstructionResolvedPiecePlacement> placements,
            IReadOnlyDictionary<int, ConstructionResolvedPiecePlacement> placementsByPieceId,
            float lowestLocalY)
        {
            _projectId = projectId;
            _blueprintId = blueprintId;
            _blueprintPieceCount = blueprintPieceCount;
            _originPosition = originPosition;
            _originRotation = originRotation;
            Placements = placements;
            PlacementsByPieceId = placementsByPieceId;
            LowestLocalY = lowestLocalY;
        }

        public IReadOnlyList<ConstructionResolvedPiecePlacement> Placements { get; }
        public IReadOnlyDictionary<int, ConstructionResolvedPiecePlacement> PlacementsByPieceId { get; }
        public float LowestLocalY { get; }

        public static CachedProjectPlacements Create(
            ConstructionProjectData project,
            StructureBlueprintData blueprint,
            IReadOnlyList<ConstructionResolvedPiecePlacement> placements)
        {
            var placementsByPieceId = new Dictionary<int, ConstructionResolvedPiecePlacement>();
            foreach (var placement in placements)
            {
                placementsByPieceId[placement.PieceId] = placement;
            }

            var lowestLocalY = 0f;
            if (blueprint.Pieces.Count > 0)
            {
                lowestLocalY = blueprint.Pieces[0].LocalPosition.y;
                for (var index = 1; index < blueprint.Pieces.Count; index++)
                {
                    var y = blueprint.Pieces[index].LocalPosition.y;
                    if (y < lowestLocalY)
                    {
                        lowestLocalY = y;
                    }
                }
            }

            return new CachedProjectPlacements(
                project.Id,
                project.BlueprintId,
                blueprint.Pieces.Count,
                project.OriginPosition,
                project.OriginRotation,
                new List<ConstructionResolvedPiecePlacement>(placements),
                placementsByPieceId,
                lowestLocalY);
        }

        public bool Matches(ConstructionProjectData project, StructureBlueprintData blueprint)
        {
            return _projectId == project.Id &&
                   _blueprintId == project.BlueprintId &&
                   _blueprintPieceCount == blueprint.Pieces.Count &&
                   _originPosition == project.OriginPosition &&
                   _originRotation == project.OriginRotation;
        }
    }
}
