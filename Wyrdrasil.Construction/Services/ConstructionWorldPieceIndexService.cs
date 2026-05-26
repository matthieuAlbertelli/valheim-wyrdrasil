using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Wyrdrasil.Construction.Components;
using Wyrdrasil.Construction.Models;
using Wyrdrasil.Core.Diagnostics;

namespace Wyrdrasil.Construction.Services;

/// <summary>
/// Native-aware spatial index over Valheim build pieces. This replaces repeated GameObject.Find calls with a
/// project-local graph snapshot built from native Piece/WearNTear objects and their colliders. It also allows
/// Wyrdrasil to adopt player-built pieces that match a blueprint target instead of blindly duplicating them.
/// </summary>
public sealed class ConstructionWorldPieceIndexService
{
    private const float ProjectBoundsPadding = 8f;
    private const float MatchPositionTolerance = 0.42f;
    private const float MatchPositionToleranceSquared = MatchPositionTolerance * MatchPositionTolerance;
    private const float MatchRotationDotTolerance = 0.985f;
    private const float GraphContactRadius = 3.25f;
    private const float GraphContactRadiusSquared = GraphContactRadius * GraphContactRadius;
    private const float GridCellSize = 4f;
    private const float PassiveWorldResyncIntervalSeconds = 10f;

    private readonly Dictionary<int, ProjectIndexCache> _cacheByProjectId = new();
    private readonly Dictionary<string, string> _placementClaimOwnersByKey = new();
    private Collider[] _overlapBuffer = new Collider[2048];

    public ProjectWorldPieceIndex GetOrBuildProjectIndex(
        ConstructionProjectData project,
        IReadOnlyList<ConstructionResolvedPiecePlacement> placements)
    {
        if (_cacheByProjectId.TryGetValue(project.Id, out var cached) && cached.Matches(project, placements, Time.time))
        {
            return cached.Index;
        }

        using (WyrdrasilProfiler.Sample("Construction.WorldPieceIndex.Build"))
        {
            var projectBounds = CalculateProjectBounds(project, placements);
            var pieces = QueryNativePieces(projectBounds);
            var nodes = new List<ConstructionWorldPieceNode>();

            foreach (var piece in pieces)
            {
                if (piece == null || piece.gameObject == null)
                {
                    continue;
                }

                var node = ConstructionWorldPieceNode.TryCreate(piece);
                if (node == null)
                {
                    continue;
                }

                if (!projectBounds.Intersects(node.Bounds) && !projectBounds.Contains(node.Position))
                {
                    continue;
                }

                nodes.Add(node);
                RegisterExistingNodeClaim(node);
            }

            var index = ProjectWorldPieceIndex.Create(project.Id, nodes, _placementClaimOwnersByKey);
            _cacheByProjectId[project.Id] = new ProjectIndexCache(
                project,
                placements,
                index,
                Time.time + PassiveWorldResyncIntervalSeconds);
            return index;
        }
    }


    private IReadOnlyCollection<Piece> QueryNativePieces(Bounds projectBounds)
    {
        using (WyrdrasilProfiler.Sample("Construction.WorldPieceIndex.QueryNativePieces"))
        {
            while (true)
            {
                var count = Physics.OverlapBoxNonAlloc(
                    projectBounds.center,
                    projectBounds.extents,
                    _overlapBuffer,
                    Quaternion.identity,
                    ~0,
                    QueryTriggerInteraction.Ignore);

                if (count < _overlapBuffer.Length)
                {
                    var pieces = new HashSet<Piece>();
                    for (var index = 0; index < count; index++)
                    {
                        var collider = _overlapBuffer[index];
                        _overlapBuffer[index] = null!;
                        if (collider == null)
                        {
                            continue;
                        }

                        var piece = collider.GetComponentInParent<Piece>();
                        if (piece != null)
                        {
                            pieces.Add(piece);
                        }
                    }

                    return pieces;
                }

                _overlapBuffer = new Collider[_overlapBuffer.Length * 2];
            }
        }
    }

    public void InvalidateProject(int projectId)
    {
        _cacheByProjectId.Remove(projectId);
    }

    public void RegisterBuiltPiece(
        ConstructionProjectData project,
        ConstructionResolvedPiecePlacement placement,
        GameObject instance)
    {
        if (instance == null)
        {
            return;
        }

        AttachMarker(instance, project.Id, placement.PieceId, project.BlueprintId, placement.PrefabName);
        RegisterPlacementClaim(project.Id, placement.PieceId, placement);
        UpsertWorldPieceIntoCachedIndexes(instance);
    }

    public bool AdoptExistingPiece(
        ConstructionProjectData project,
        ConstructionResolvedPiecePlacement placement,
        ConstructionWorldPieceMatch match)
    {
        if (match.GameObject == null || match.Node == null)
        {
            return false;
        }

        if (match.Node.IsReservedForOtherProjectPiece(project.Id, placement.PieceId))
        {
            return false;
        }

        if (IsPlacementClaimedByOtherProjectPiece(project.Id, placement.PieceId, placement))
        {
            return false;
        }

        AttachMarker(match.GameObject, project.Id, placement.PieceId, project.BlueprintId, placement.PrefabName);
        RegisterPlacementClaim(project.Id, placement.PieceId, placement);
        UpsertWorldPieceIntoCachedIndexes(match.GameObject);
        return true;
    }


    private void RegisterPlacementClaim(int projectId, int pieceId, ConstructionResolvedPiecePlacement placement)
    {
        _placementClaimOwnersByKey[CreatePlacementClaimKey(placement)] = CreateClaimOwner(projectId, pieceId);
    }

    private void RegisterExistingNodeClaim(ConstructionWorldPieceNode node)
    {
        if (!node.ProjectId.HasValue || !node.PieceId.HasValue)
        {
            return;
        }

        var claimKey = CreatePlacementClaimKey(node.PrefabName, node.Position, node.Rotation);
        if (_placementClaimOwnersByKey.ContainsKey(claimKey))
        {
            return;
        }

        _placementClaimOwnersByKey[claimKey] = CreateClaimOwner(node.ProjectId.Value, node.PieceId.Value);
    }

    private bool IsPlacementClaimedByOtherProjectPiece(
        int projectId,
        int pieceId,
        ConstructionResolvedPiecePlacement placement)
    {
        if (!_placementClaimOwnersByKey.TryGetValue(CreatePlacementClaimKey(placement), out var owner))
        {
            return false;
        }

        return !string.Equals(owner, CreateClaimOwner(projectId, pieceId), StringComparison.Ordinal);
    }

    private static string CreateClaimOwner(int projectId, int pieceId)
    {
        return projectId.ToString() + ":" + pieceId.ToString();
    }

    private static string CreatePlacementClaimKey(ConstructionResolvedPiecePlacement placement)
    {
        return CreatePlacementClaimKey(placement.PrefabName, placement.WorldPosition, placement.WorldRotation);
    }

    private static string CreatePlacementClaimKey(string prefabName, Vector3 position, Quaternion rotation)
    {
        return NormalizePrefabName(prefabName) + "@" +
               Quantize(position.x, MatchPositionTolerance) + ":" +
               Quantize(position.y, MatchPositionTolerance) + ":" +
               Quantize(position.z, MatchPositionTolerance) + "@" +
               Quantize(rotation.x, 0.01f) + ":" +
               Quantize(rotation.y, 0.01f) + ":" +
               Quantize(rotation.z, 0.01f) + ":" +
               Quantize(rotation.w, 0.01f);
    }

    private static int Quantize(float value, float step)
    {
        return Mathf.RoundToInt(value / step);
    }


    private void UpsertWorldPieceIntoCachedIndexes(GameObject instance)
    {
        RefreshCachedClaimSnapshots();

        var piece = instance.GetComponent<Piece>();
        if (piece == null)
        {
            piece = instance.GetComponentInParent<Piece>();
        }

        if (piece == null)
        {
            // This should not happen for Valheim construction pieces, but if a prefab does not expose
            // Piece we must not keep stale graph snapshots around. Marking dirty keeps the fallback safe.
            MarkAllCachesDirty();
            return;
        }

        var node = ConstructionWorldPieceNode.TryCreate(piece);
        if (node == null)
        {
            MarkAllCachesDirty();
            return;
        }

        foreach (var cached in _cacheByProjectId.Values)
        {
            cached.Index.UpsertNode(node);
        }
    }

    private void RefreshCachedClaimSnapshots()
    {
        foreach (var cached in _cacheByProjectId.Values)
        {
            cached.Index.RefreshPlacementClaimOwners(_placementClaimOwnersByKey);
        }
    }

    private void MarkAllCachesDirty()
    {
        foreach (var cached in _cacheByProjectId.Values)
        {
            cached.MarkDirty();
        }
    }

    private static void AttachMarker(GameObject instance, int projectId, int pieceId, string blueprintId, string prefabName)
    {
        var marker = instance.GetComponent<WyrdrasilConstructedPieceMarker>();
        if (marker == null)
        {
            marker = instance.AddComponent<WyrdrasilConstructedPieceMarker>();
        }

        marker.Initialize(projectId, pieceId, blueprintId, prefabName);
    }

    private static Bounds CalculateProjectBounds(
        ConstructionProjectData project,
        IReadOnlyList<ConstructionResolvedPiecePlacement> placements)
    {
        if (placements.Count == 0)
        {
            var empty = new Bounds(project.OriginPosition, Vector3.one * ProjectBoundsPadding);
            return empty;
        }

        var bounds = new Bounds(placements[0].WorldPosition, Vector3.one);
        for (var index = 1; index < placements.Count; index++)
        {
            bounds.Encapsulate(placements[index].WorldPosition);
        }

        bounds.Expand(ProjectBoundsPadding);
        return bounds;
    }

    private sealed class ProjectIndexCache
    {
        private readonly string _blueprintId;
        private readonly Vector3 _originPosition;
        private readonly Quaternion _originRotation;
        private readonly int _placementCount;

        public ProjectIndexCache(
            ConstructionProjectData project,
            IReadOnlyCollection<ConstructionResolvedPiecePlacement> placements,
            ProjectWorldPieceIndex index,
            float nextPassiveWorldResyncAt)
        {
            _blueprintId = project.BlueprintId;
            _originPosition = project.OriginPosition;
            _originRotation = project.OriginRotation;
            _placementCount = placements.Count;
            _nextPassiveWorldResyncAt = nextPassiveWorldResyncAt;
            Index = index;
        }

        private readonly float _nextPassiveWorldResyncAt;
        private bool _isDirty;

        public ProjectWorldPieceIndex Index { get; }

        public void MarkDirty()
        {
            _isDirty = true;
        }

        public bool Matches(ConstructionProjectData project, IReadOnlyCollection<ConstructionResolvedPiecePlacement> placements, float now)
        {
            return !_isDirty &&
                   now < _nextPassiveWorldResyncAt &&
                   _blueprintId == project.BlueprintId &&
                   _originPosition == project.OriginPosition &&
                   _originRotation == project.OriginRotation &&
                   _placementCount == placements.Count;
        }
    }

    public sealed class ProjectWorldPieceIndex
    {
        private readonly int _projectId;
        private readonly Dictionary<int, ConstructionWorldPieceNode> _nodesByExactProjectPiece = new();
        private readonly Dictionary<GridKey, List<ConstructionWorldPieceNode>> _nodesByCell = new();
        private readonly Dictionary<string, string> _placementClaimOwnersByKey;
        private readonly List<ConstructionWorldPieceNode> _nodes;

        private ProjectWorldPieceIndex(
            int projectId,
            List<ConstructionWorldPieceNode> nodes,
            IReadOnlyDictionary<string, string>? placementClaimOwnersByKey)
        {
            _projectId = projectId;
            _placementClaimOwnersByKey = new Dictionary<string, string>();
            if (placementClaimOwnersByKey != null)
            {
                foreach (var claim in placementClaimOwnersByKey)
                {
                    _placementClaimOwnersByKey[claim.Key] = claim.Value;
                }
            }
            _nodes = nodes;

            foreach (var node in nodes)
            {
                if (node.ProjectId == projectId && node.PieceId.HasValue)
                {
                    _nodesByExactProjectPiece[node.PieceId.Value] = node;
                }

                var key = GridKey.FromPosition(node.Position, GridCellSize);
                if (!_nodesByCell.TryGetValue(key, out var bucket))
                {
                    bucket = new List<ConstructionWorldPieceNode>();
                    _nodesByCell[key] = bucket;
                }

                bucket.Add(node);
            }
        }

        public static ProjectWorldPieceIndex Create(
            int projectId,
            List<ConstructionWorldPieceNode> nodes,
            IReadOnlyDictionary<string, string>? placementClaimOwnersByKey = null)
        {
            return new ProjectWorldPieceIndex(projectId, nodes, placementClaimOwnersByKey);
        }

        public void RefreshPlacementClaimOwners(IReadOnlyDictionary<string, string> placementClaimOwnersByKey)
        {
            _placementClaimOwnersByKey.Clear();
            foreach (var claim in placementClaimOwnersByKey)
            {
                _placementClaimOwnersByKey[claim.Key] = claim.Value;
            }
        }

        public void UpsertNode(ConstructionWorldPieceNode node)
        {
            if (node == null || !node.IsAlive)
            {
                return;
            }

            if (node.ProjectId == _projectId && node.PieceId.HasValue)
            {
                _nodesByExactProjectPiece[node.PieceId.Value] = node;
            }

            var key = GridKey.FromPosition(node.Position, GridCellSize);
            if (!_nodesByCell.TryGetValue(key, out var bucket))
            {
                bucket = new List<ConstructionWorldPieceNode>();
                _nodesByCell[key] = bucket;
            }

            for (var index = 0; index < bucket.Count; index++)
            {
                var existing = bucket[index];
                if (existing.GameObject == node.GameObject)
                {
                    bucket[index] = node;
                    ReplaceNodeInFlatList(existing, node);
                    return;
                }
            }

            bucket.Add(node);
            _nodes.Add(node);
        }

        private void ReplaceNodeInFlatList(ConstructionWorldPieceNode existing, ConstructionWorldPieceNode replacement)
        {
            for (var index = 0; index < _nodes.Count; index++)
            {
                if (_nodes[index].GameObject == existing.GameObject)
                {
                    _nodes[index] = replacement;
                    return;
                }
            }

            _nodes.Add(replacement);
        }

        public bool IsPlacementClaimedByOtherProjectPiece(ConstructionResolvedPiecePlacement placement)
        {
            if (!_placementClaimOwnersByKey.TryGetValue(CreatePlacementClaimKey(placement), out var owner))
            {
                return false;
            }

            return !string.Equals(owner, CreateClaimOwner(_projectId, placement.PieceId), StringComparison.Ordinal);
        }

        public bool TryFindMatchingPiece(
            ConstructionResolvedPiecePlacement placement,
            out ConstructionWorldPieceMatch match)
        {
            if (IsPlacementClaimedByOtherProjectPiece(placement))
            {
                match = ConstructionWorldPieceMatch.Empty;
                return false;
            }

            if (_nodesByExactProjectPiece.TryGetValue(placement.PieceId, out var exactNode) &&
                exactNode.IsAlive &&
                !exactNode.IsReservedForOtherProjectPiece(_projectId, placement.PieceId))
            {
                match = new ConstructionWorldPieceMatch(exactNode, true, 0f);
                return true;
            }

            var normalizedPrefabName = NormalizePrefabName(placement.PrefabName);
            ConstructionWorldPieceNode? bestNode = null;
            var bestDistanceSquared = float.MaxValue;

            foreach (var node in EnumerateNearbyNodes(placement.WorldPosition, MatchPositionTolerance))
            {
                if (!node.IsAlive || node.IsReservedForOtherProjectPiece(_projectId, placement.PieceId))
                {
                    continue;
                }

                if (!string.Equals(node.NormalizedPrefabName, normalizedPrefabName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var distanceSquared = (node.Position - placement.WorldPosition).sqrMagnitude;
                if (distanceSquared > MatchPositionToleranceSquared || distanceSquared >= bestDistanceSquared)
                {
                    continue;
                }

                if (!HasCompatibleRotation(node.Rotation, placement.WorldRotation))
                {
                    continue;
                }

                bestNode = node;
                bestDistanceSquared = distanceSquared;
            }

            if (bestNode != null)
            {
                match = new ConstructionWorldPieceMatch(bestNode, false, Mathf.Sqrt(bestDistanceSquared));
                return true;
            }

            match = ConstructionWorldPieceMatch.Empty;
            return false;
        }

        public bool HasNativeConnectionNear(
            ConstructionResolvedPiecePlacement placement,
            IEnumerable<int> builtPieceIds,
            IReadOnlyDictionary<int, ConstructionResolvedPiecePlacement> placementsByPieceId)
        {
            foreach (var node in EnumerateNearbyNodes(placement.WorldPosition, GraphContactRadius))
            {
                if (!node.IsAlive || node.IsReservedForOtherProjectPiece(_projectId, placement.PieceId))
                {
                    continue;
                }

                if (node.ProjectId == _projectId && node.PieceId == placement.PieceId)
                {
                    continue;
                }

                if (node.BoundsDistanceSquaredTo(placement.WorldPosition) <= GraphContactRadiusSquared)
                {
                    return true;
                }
            }

            foreach (var builtPieceId in builtPieceIds)
            {
                if (!placementsByPieceId.TryGetValue(builtPieceId, out var builtPlacement))
                {
                    continue;
                }

                if ((builtPlacement.WorldPosition - placement.WorldPosition).sqrMagnitude <= GraphContactRadiusSquared)
                {
                    return true;
                }
            }

            return false;
        }

        private IEnumerable<ConstructionWorldPieceNode> EnumerateNearbyNodes(Vector3 position, float radius)
        {
            if (_nodes.Count == 0)
            {
                yield break;
            }

            var radiusInCells = Mathf.Max(1, Mathf.CeilToInt(radius / GridCellSize));
            var center = GridKey.FromPosition(position, GridCellSize);
            for (var x = center.X - radiusInCells; x <= center.X + radiusInCells; x++)
            {
                for (var y = center.Y - radiusInCells; y <= center.Y + radiusInCells; y++)
                {
                    for (var z = center.Z - radiusInCells; z <= center.Z + radiusInCells; z++)
                    {
                        if (!_nodesByCell.TryGetValue(new GridKey(x, y, z), out var bucket))
                        {
                            continue;
                        }

                        foreach (var node in bucket)
                        {
                            yield return node;
                        }
                    }
                }
            }
        }

        private static bool HasCompatibleRotation(Quaternion actual, Quaternion expected)
        {
            var dot = Mathf.Abs(Quaternion.Dot(actual, expected));
            return dot >= MatchRotationDotTolerance;
        }
    }

    public sealed class ConstructionWorldPieceNode
    {
        private ConstructionWorldPieceNode(
            Piece piece,
            GameObject gameObject,
            Vector3 position,
            Quaternion rotation,
            Bounds bounds,
            string prefabName,
            int? projectId,
            int? pieceId)
        {
            Piece = piece;
            GameObject = gameObject;
            Position = position;
            Rotation = rotation;
            Bounds = bounds;
            PrefabName = prefabName;
            NormalizedPrefabName = NormalizePrefabName(prefabName);
            ProjectId = projectId;
            PieceId = pieceId;
        }

        public Piece Piece { get; }
        public GameObject GameObject { get; }
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public Bounds Bounds { get; }
        public string PrefabName { get; }
        public string NormalizedPrefabName { get; }
        public int? ProjectId { get; }
        public int? PieceId { get; }
        public bool IsAlive => GameObject != null;

        public static ConstructionWorldPieceNode? TryCreate(Piece piece)
        {
            var gameObject = piece.gameObject;
            var marker = gameObject.GetComponent<WyrdrasilConstructedPieceMarker>();
            int? projectId = null;
            int? pieceId = null;
            string prefabName;

            if (marker != null && marker.ProjectId > 0 && marker.PieceId > 0)
            {
                projectId = marker.ProjectId;
                pieceId = marker.PieceId;
                prefabName = string.IsNullOrWhiteSpace(marker.PrefabName)
                    ? ExtractPrefabName(gameObject.name)
                    : marker.PrefabName;
            }
            else if (TryParseWyrdrasilBuiltName(gameObject.name, out var parsedProjectId, out var parsedPieceId, out var parsedPrefabName))
            {
                projectId = parsedProjectId;
                pieceId = parsedPieceId;
                prefabName = parsedPrefabName;
            }
            else
            {
                prefabName = ExtractPrefabName(gameObject.name);
            }

            if (string.IsNullOrWhiteSpace(prefabName))
            {
                return null;
            }

            return new ConstructionWorldPieceNode(
                piece,
                gameObject,
                gameObject.transform.position,
                gameObject.transform.rotation,
                CalculateBounds(gameObject),
                prefabName,
                projectId,
                pieceId);
        }

        public bool IsReservedForOtherProjectPiece(int projectId, int pieceId)
        {
            if (GameObject != null)
            {
                var liveMarker = GameObject.GetComponent<WyrdrasilConstructedPieceMarker>();
                if (liveMarker != null && liveMarker.ProjectId > 0 && liveMarker.PieceId > 0)
                {
                    return liveMarker.ProjectId != projectId || liveMarker.PieceId != pieceId;
                }
            }

            if (!ProjectId.HasValue || !PieceId.HasValue)
            {
                return false;
            }

            return ProjectId.Value != projectId || PieceId.Value != pieceId;
        }

        public float BoundsDistanceSquaredTo(Vector3 position)
        {
            var closest = Bounds.ClosestPoint(position);
            return (closest - position).sqrMagnitude;
        }

        private static Bounds CalculateBounds(GameObject gameObject)
        {
            var initialized = false;
            var bounds = new Bounds(gameObject.transform.position, Vector3.one * 0.5f);

            foreach (var collider in gameObject.GetComponentsInChildren<Collider>(true))
            {
                if (collider == null || collider.isTrigger)
                {
                    continue;
                }

                if (!initialized)
                {
                    bounds = collider.bounds;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }

            if (initialized)
            {
                return bounds;
            }

            foreach (var renderer in gameObject.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null)
                {
                    continue;
                }

                if (!initialized)
                {
                    bounds = renderer.bounds;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return initialized ? bounds : new Bounds(gameObject.transform.position, Vector3.one * 0.5f);
        }
    }

    public readonly struct ConstructionWorldPieceMatch
    {
        public static readonly ConstructionWorldPieceMatch Empty = new(null, false, 0f);

        public ConstructionWorldPieceMatch(ConstructionWorldPieceNode? node, bool exactProjectPieceMatch, float distance)
        {
            Node = node;
            ExactProjectPieceMatch = exactProjectPieceMatch;
            Distance = distance;
        }

        public ConstructionWorldPieceNode? Node { get; }
        public bool ExactProjectPieceMatch { get; }
        public float Distance { get; }
        public GameObject? GameObject => Node?.GameObject;
        public string WorldObjectName => GameObject == null ? string.Empty : GameObject.name;
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

    private static bool TryParseWyrdrasilBuiltName(
        string objectName,
        out int projectId,
        out int pieceId,
        out string prefabName)
    {
        projectId = 0;
        pieceId = 0;
        prefabName = string.Empty;

        objectName = ExtractPrefabName(objectName);
        const string prefix = "Wyrdrasil_ConstructionProject_";
        const string pieceSeparator = "_Piece_";
        if (!objectName.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var remaining = objectName.Substring(prefix.Length);
        var separatorIndex = remaining.IndexOf(pieceSeparator, StringComparison.Ordinal);
        if (separatorIndex <= 0)
        {
            return false;
        }

        if (!int.TryParse(remaining.Substring(0, separatorIndex), out projectId))
        {
            return false;
        }

        var afterPiece = remaining.Substring(separatorIndex + pieceSeparator.Length);
        var nextSeparatorIndex = afterPiece.IndexOf('_');
        if (nextSeparatorIndex <= 0)
        {
            return false;
        }

        if (!int.TryParse(afterPiece.Substring(0, nextSeparatorIndex), out pieceId))
        {
            return false;
        }

        prefabName = afterPiece.Substring(nextSeparatorIndex + 1);
        return !string.IsNullOrWhiteSpace(prefabName);
    }

    private static string ExtractPrefabName(string gameObjectName)
    {
        if (string.IsNullOrWhiteSpace(gameObjectName))
        {
            return string.Empty;
        }

        const string cloneSuffix = "(Clone)";
        if (gameObjectName.EndsWith(cloneSuffix, StringComparison.Ordinal))
        {
            gameObjectName = gameObjectName.Substring(0, gameObjectName.Length - cloneSuffix.Length);
        }

        return gameObjectName.Trim();
    }

    private static string NormalizePrefabName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return new string(value
                .Where(character => character != '_' && character != '-' && !char.IsWhiteSpace(character))
                .ToArray())
            .ToLowerInvariant();
    }
}
