using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using UnityEngine;
using Wyrdrasil.Core.Services;
using Wyrdrasil.Settlements.Components;
using Wyrdrasil.Settlements.Tool;

namespace Wyrdrasil.Settlements.Services;

public sealed class BuildingService
{
    private const float DefaultBuildingHeight = 6f;
    private const float VerticalSnapStep = 0.5f;
    private const float SurfaceUpDotThreshold = 0.55f;
    private const float CloseFootprintDistance = 1f;
    private const float MinPointSpacing = 0.75f;
    private const float MinBuildingHeight = 1f;
    private const float PreviewYOffset = 0.08f;
    private const float BuildingRaySelectionMaxDistance = 100f;
    private const float BuildingRaySelectionStep = 0.35f;

    private readonly ManualLogSource _log;
    private readonly List<BuildingData> _buildings = new();
    private readonly Dictionary<int, WyrdrasilBuildingMarker> _markers = new();
    private readonly Dictionary<int, GameObject> _buildingRoots = new();
    private readonly List<Vector3> _pendingFootprintPoints = new();
    private readonly List<GameObject> _pendingPointVisuals = new();
    private readonly List<LineRenderer> _pendingHeightEdges = new();

    private GameObject? _pendingRoot;
    private LineRenderer? _pendingFootprintLine;
    private LineRenderer? _pendingBaseLine;
    private LineRenderer? _pendingTopLine;
    private Vector3? _pendingGhostPoint;
    private float _pendingBaseY;
    private float _pendingTopY;
    private int _nextBuildingId = 1;
    private int? _highlightedBuildingId;
    private bool _registryModeVisualsVisible;
    private bool _playerAuthoringVisualsVisible;
    private bool _visualsVisible;

    public IReadOnlyList<BuildingData> Buildings => _buildings;
    public int NextBuildingId => _nextBuildingId;
    public bool IsBuildingAuthoringActive => AuthoringPhase != ZoneAuthoringPhase.None;
    public bool IsBuildingHeightEditingActive => AuthoringPhase == ZoneAuthoringPhase.Height;
    public ZoneAuthoringPhase AuthoringPhase { get; private set; }

    public BuildingService(ManualLogSource log, RegistryModeService modeService)
    {
        _log = log;
        _registryModeVisualsVisible = modeService.IsRegistryModeEnabled;
        RefreshVisualsVisible();
        modeService.RegistryModeChanged += OnRegistryModeChanged;
    }

    public PendingZoneAuthoringSnapshot? GetPendingBuildingAuthoringSnapshot()
    {
        if (!IsBuildingAuthoringActive)
        {
            return null;
        }

        return new PendingZoneAuthoringSnapshot(
            AuthoringPhase,
            _pendingFootprintPoints.Count,
            _pendingBaseY,
            _pendingTopY,
            CanClosePendingFootprint());
    }

    public bool HandleBuildingAuthoringPrimaryInput()
    {
        if (AuthoringPhase == ZoneAuthoringPhase.Height)
        {
            return FinalizePendingBuilding() != null;
        }

        if (!TryGetPlacementPoint(out var placementPoint))
        {
            _log.LogWarning("Cannot place point for building: no valid support surface was found.");
            return false;
        }

        if (AuthoringPhase == ZoneAuthoringPhase.None)
        {
            BeginBuildingAuthoring();
        }

        if (CanClosePendingFootprint(placementPoint))
        {
            BeginBuildingHeightEditing();
            return false;
        }

        TryAddPendingFootprintPoint(placementPoint);
        return false;
    }

    public void UpdatePendingBuildingAuthoringPreview()
    {
        if (AuthoringPhase != ZoneAuthoringPhase.Footprint)
        {
            return;
        }

        _pendingGhostPoint = TryGetPlacementPoint(out var placementPoint)
            ? CanClosePendingFootprint(placementPoint) ? _pendingFootprintPoints[0] : placementPoint
            : null;

        UpdatePendingPreviewVisuals();
    }

    public void HandleBuildingAuthoringSecondaryInput()
    {
        if (AuthoringPhase == ZoneAuthoringPhase.None)
        {
            return;
        }

        if (AuthoringPhase == ZoneAuthoringPhase.Height)
        {
            CancelPendingBuildingAuthoring();
            return;
        }

        if (_pendingFootprintPoints.Count > 0)
        {
            _pendingFootprintPoints.RemoveAt(_pendingFootprintPoints.Count - 1);
            if (_pendingFootprintPoints.Count == 0)
            {
                CancelPendingBuildingAuthoring();
                return;
            }

            UpdatePendingPreviewVisuals();
        }
    }

    public void AdjustPendingBuildingHeight(int direction, bool adjustBase)
    {
        if (AuthoringPhase != ZoneAuthoringPhase.Height || direction == 0)
        {
            return;
        }

        var delta = direction > 0 ? VerticalSnapStep : -VerticalSnapStep;
        if (adjustBase)
        {
            var nextBase = SnapHeight(_pendingBaseY + delta);
            if (_pendingTopY - nextBase < MinBuildingHeight)
            {
                nextBase = _pendingTopY - MinBuildingHeight;
            }

            _pendingBaseY = SnapHeight(nextBase);
        }
        else
        {
            var nextTop = SnapHeight(_pendingTopY + delta);
            if (nextTop - _pendingBaseY < MinBuildingHeight)
            {
                nextTop = _pendingBaseY + MinBuildingHeight;
            }

            _pendingTopY = SnapHeight(nextTop);
        }

        UpdatePendingPreviewVisuals();
    }

    public void CancelPendingBuildingAuthoring()
    {
        _pendingFootprintPoints.Clear();
        _pendingGhostPoint = null;
        _pendingBaseY = 0f;
        _pendingTopY = 0f;
        AuthoringPhase = ZoneAuthoringPhase.None;
        DestroyPendingPreviewVisuals();
    }

    public void SetPlayerAuthoringVisualsVisible(bool visible)
    {
        if (_playerAuthoringVisualsVisible == visible)
        {
            return;
        }

        _playerAuthoringVisualsVisible = visible;
        RefreshVisualsVisible();
    }

    public void LoadBuildings(IEnumerable<BuildingData> buildings, int nextBuildingId)
    {
        CancelPendingBuildingAuthoring();
        foreach (var root in _buildingRoots.Values)
        {
            if (root != null)
            {
                Object.Destroy(root);
            }
        }

        _buildings.Clear();
        _buildingRoots.Clear();
        _markers.Clear();

        foreach (var building in buildings)
        {
            _buildings.Add(building);
            CreateBuildingWorldObject(building);
        }

        _nextBuildingId = nextBuildingId;
    }

    public void ClearAllBuildings()
    {
        CancelPendingBuildingAuthoring();
        foreach (var root in _buildingRoots.Values)
        {
            if (root != null)
            {
                Object.Destroy(root);
            }
        }

        _buildings.Clear();
        _buildingRoots.Clear();
        _markers.Clear();
        _highlightedBuildingId = null;
        _nextBuildingId = 1;
    }

    public BuildingData CreateImplicitBuildingForZone(ZoneType zoneType, Vector3 anchorPosition)
    {
        return CreateImplicitBuilding($"{zoneType} Building", anchorPosition, $"zone type '{zoneType}'");
    }

    public BuildingData CreateImplicitBuildingForDesignation(string designationLabel, Vector3 anchorPosition)
    {
        return CreateImplicitBuilding($"{designationLabel} Building", anchorPosition, $"standalone designation '{designationLabel}'");
    }

    public bool TryFindBuildingAtPoint(Vector3 point, out BuildingData building)
    {
        var match = FindBuildingContainingPoint(point);
        if (match != null)
        {
            building = match;
            return true;
        }

        building = null!;
        return false;
    }

    public bool TryGetBuildingAtCrosshair(out BuildingData building)
    {
        if (TryFindBuildingAlongViewRay(out building))
        {
            return true;
        }

        if (TryGetPlacementPoint(out var placementPoint) && TryFindBuildingAtPoint(placementPoint, out building))
        {
            return true;
        }

        building = null!;
        return false;
    }

    public BuildingData? FindBuildingContainingPoint(Vector3 point) =>
        _buildings.Where(building => building.ContainsPoint(point))
                  .OrderBy(building => Vector2.Distance(new Vector2(building.AnchorPosition.x, building.AnchorPosition.z), new Vector2(point.x, point.z)))
                  .FirstOrDefault();

    private bool TryFindBuildingAlongViewRay(out BuildingData building)
    {
        building = null!;

        var activeCamera = Camera.main;
        if (activeCamera == null)
        {
            return false;
        }

        var ray = new Ray(activeCamera.transform.position, activeCamera.transform.forward);
        var buildingsWithVolume = _buildings.Where(candidate => candidate.HasVolume).ToArray();
        if (buildingsWithVolume.Length == 0)
        {
            return false;
        }

        for (var distance = 0f; distance <= BuildingRaySelectionMaxDistance; distance += BuildingRaySelectionStep)
        {
            var samplePoint = ray.GetPoint(distance);
            var match = buildingsWithVolume
                .Where(candidate => candidate.ContainsPoint(samplePoint))
                .OrderBy(candidate => EstimateFootprintArea(candidate))
                .FirstOrDefault();

            if (match == null)
            {
                continue;
            }

            building = match;
            return true;
        }

        return false;
    }

    private static float EstimateFootprintArea(BuildingData building)
    {
        var points = building.FootprintPoints;
        if (points.Count < 3)
        {
            return float.MaxValue;
        }

        var area = 0f;
        for (var i = 0; i < points.Count; i++)
        {
            var a = points[i];
            var b = points[(i + 1) % points.Count];
            area += (a.x * b.y) - (b.x * a.y);
        }

        return Mathf.Abs(area) * 0.5f;
    }

    public void UpdateTargetedBuildingHighlight()
    {
        if (!_visualsVisible)
        {
            SetHighlightedBuilding(null);
            return;
        }

        if (!TryGetBuildingAtCrosshair(out var building))
        {
            SetHighlightedBuilding(null);
            return;
        }

        SetHighlightedBuilding(building.Id);
    }

    public bool DeleteBuildingIfUnused(
        int buildingId,
        IReadOnlyList<FunctionalZoneData> zones,
        IReadOnlyList<ZoneSlotData> slots,
        IReadOnlyList<RegisteredSeatData> seats,
        IReadOnlyList<RegisteredBedData> beds,
        IReadOnlyList<RegisteredCraftStationData> craftStations)
    {
        if (zones.Any(zone => zone.BuildingId == buildingId) ||
            slots.Any(slot => slot.BuildingId == buildingId) ||
            seats.Any(seat => seat.BuildingId == buildingId) ||
            beds.Any(bed => bed.BuildingId == buildingId) ||
            craftStations.Any(station => station.BuildingId == buildingId))
        {
            return false;
        }

        var removed = _buildings.RemoveAll(building => building.Id == buildingId) > 0;
        if (removed)
        {
            if (_buildingRoots.TryGetValue(buildingId, out var root) && root != null)
            {
                Object.Destroy(root);
            }

            _buildingRoots.Remove(buildingId);
            _markers.Remove(buildingId);
            SetHighlightedBuilding(_highlightedBuildingId == buildingId ? null : _highlightedBuildingId);
            _log.LogInfo($"Deleted orphaned building #{buildingId}.");
        }

        return removed;
    }

    public bool TryGetPlacementPoint(out Vector3 placementPoint)
    {
        var localPlayer = Player.m_localPlayer;
        if (!localPlayer)
        {
            placementPoint = Vector3.zero;
            return false;
        }

        var activeCamera = Camera.main;
        if (activeCamera != null)
        {
            var ray = new Ray(activeCamera.transform.position, activeCamera.transform.forward);
            if (Physics.Raycast(ray, out var hitInfo, 100f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!IsValidSupportSurface(hitInfo.normal))
                {
                    placementPoint = Vector3.zero;
                    return false;
                }

                placementPoint = hitInfo.point;
                placementPoint.y += 0.05f;
                return true;
            }
        }

        placementPoint = Vector3.zero;
        return false;
    }

    private void OnRegistryModeChanged(bool isEnabled)
    {
        _registryModeVisualsVisible = isEnabled;
        RefreshVisualsVisible();

        if (!isEnabled && !_playerAuthoringVisualsVisible)
        {
            CancelPendingBuildingAuthoring();
            SetHighlightedBuilding(null);
        }
    }

    private void RefreshVisualsVisible()
    {
        var visible = _registryModeVisualsVisible || _playerAuthoringVisualsVisible;
        if (_visualsVisible == visible)
        {
            return;
        }

        _visualsVisible = visible;
        foreach (var marker in _markers.Values)
        {
            marker.SetVisualizationVisible(visible);
        }

        if (IsBuildingAuthoringActive)
        {
            UpdatePendingPreviewVisuals();
        }

        if (!visible)
        {
            SetHighlightedBuilding(null);
        }
    }

    private BuildingData CreateImplicitBuilding(string label, Vector3 anchorPosition, string logContext)
    {
        var building = new BuildingData(
            _nextBuildingId++,
            $"{label} #{_nextBuildingId - 1}",
            anchorPosition);

        _buildings.Add(building);
        _log.LogInfo($"Created implicit building #{building.Id} for {logContext} at {building.AnchorPosition}.");
        return building;
    }

    private void BeginBuildingAuthoring()
    {
        AuthoringPhase = ZoneAuthoringPhase.Footprint;
        EnsurePendingPreviewVisuals();
        UpdatePendingPreviewVisuals();
    }

    private void BeginBuildingHeightEditing()
    {
        var minY = _pendingFootprintPoints.Min(point => point.y);
        _pendingBaseY = SnapHeight(minY);
        _pendingTopY = SnapHeight(_pendingBaseY + DefaultBuildingHeight);
        AuthoringPhase = ZoneAuthoringPhase.Height;
        _pendingGhostPoint = null;
        UpdatePendingPreviewVisuals();
    }

    private BuildingData? FinalizePendingBuilding()
    {
        if (_pendingFootprintPoints.Count < 3)
        {
            return null;
        }

        var anchorPosition = ComputeAnchorPosition(_pendingFootprintPoints);
        var building = new BuildingData(
            _nextBuildingId++,
            $"Bâtiment #{_nextBuildingId - 1}",
            anchorPosition,
            _pendingFootprintPoints.Select(point => new Vector2(point.x, point.z)),
            _pendingBaseY,
            _pendingTopY,
            0);

        _buildings.Add(building);
        CreateBuildingWorldObject(building);
        CancelPendingBuildingAuthoring();
        _log.LogInfo($"Created building capture volume #{building.Id} at {building.AnchorPosition}.");
        return building;
    }

    private bool TryAddPendingFootprintPoint(Vector3 point)
    {
        if (_pendingFootprintPoints.Count > 0 && Vector3.Distance(_pendingFootprintPoints[_pendingFootprintPoints.Count - 1], point) < MinPointSpacing)
        {
            return false;
        }

        if (WouldNewFootprintSegmentSelfIntersect(point))
        {
            return false;
        }

        _pendingFootprintPoints.Add(point);
        UpdatePendingPreviewVisuals();
        return true;
    }

    private bool CanClosePendingFootprint() => _pendingFootprintPoints.Count >= 3;

    private bool CanClosePendingFootprint(Vector3 candidatePoint)
    {
        if (_pendingFootprintPoints.Count < 3)
        {
            return false;
        }

        var firstPoint = _pendingFootprintPoints[0];
        return Vector2.Distance(new Vector2(candidatePoint.x, candidatePoint.z), new Vector2(firstPoint.x, firstPoint.z)) <= CloseFootprintDistance
               && !WouldClosingSegmentSelfIntersect();
    }

    private bool WouldNewFootprintSegmentSelfIntersect(Vector3 candidatePoint)
    {
        if (_pendingFootprintPoints.Count < 2)
        {
            return false;
        }

        var segmentStart = ToPoint2D(_pendingFootprintPoints[_pendingFootprintPoints.Count - 1]);
        var segmentEnd = ToPoint2D(candidatePoint);
        for (var i = 0; i < _pendingFootprintPoints.Count - 2; i++)
        {
            if (DoSegmentsIntersect(segmentStart, segmentEnd, ToPoint2D(_pendingFootprintPoints[i]), ToPoint2D(_pendingFootprintPoints[i + 1])))
            {
                return true;
            }
        }

        return false;
    }

    private bool WouldClosingSegmentSelfIntersect()
    {
        if (_pendingFootprintPoints.Count < 3)
        {
            return false;
        }

        var segmentStart = ToPoint2D(_pendingFootprintPoints[_pendingFootprintPoints.Count - 1]);
        var segmentEnd = ToPoint2D(_pendingFootprintPoints[0]);
        for (var i = 1; i < _pendingFootprintPoints.Count - 2; i++)
        {
            if (DoSegmentsIntersect(segmentStart, segmentEnd, ToPoint2D(_pendingFootprintPoints[i]), ToPoint2D(_pendingFootprintPoints[i + 1])))
            {
                return true;
            }
        }

        return false;
    }

    private void CreateBuildingWorldObject(BuildingData building)
    {
        if (!building.HasVolume)
        {
            return;
        }

        var root = new GameObject($"Wyrdrasil_Building_{building.Id}");
        root.transform.position = building.AnchorPosition;
        _buildingRoots[building.Id] = root;
        var marker = root.AddComponent<WyrdrasilBuildingMarker>();
        marker.Initialize(building.Id, building.BaseY, building.TopY, building.FootprintPoints.Count);

        var baseObject = new GameObject("BuildingBaseOutline");
        baseObject.transform.SetParent(root.transform, false);
        var baseLine = CreateWorldLineRenderer(baseObject, true, 0.1f, new Color(0.15f, 0.65f, 1f, 1f));
        ApplyClosedLine(baseLine, building.FootprintPoints.Select(point => new Vector3(point.x, building.BaseY + PreviewYOffset, point.y)).ToArray());
        marker.RegisterRenderer(baseLine);

        var topObject = new GameObject("BuildingTopOutline");
        topObject.transform.SetParent(root.transform, false);
        var topLine = CreateWorldLineRenderer(topObject, true, 0.07f, new Color(0.25f, 0.9f, 1f, 0.95f));
        ApplyClosedLine(topLine, building.FootprintPoints.Select(point => new Vector3(point.x, building.TopY + PreviewYOffset, point.y)).ToArray());
        marker.RegisterRenderer(topLine);

        for (var i = 0; i < building.FootprintPoints.Count; i++)
        {
            var edgeObject = new GameObject($"BuildingHeightEdge_{i}");
            edgeObject.transform.SetParent(root.transform, false);
            var edge = CreateWorldLineRenderer(edgeObject, false, 0.035f, new Color(0.25f, 0.9f, 1f, 0.85f));
            edge.positionCount = 2;
            var point = building.FootprintPoints[i];
            edge.SetPosition(0, new Vector3(point.x, building.BaseY + PreviewYOffset, point.y));
            edge.SetPosition(1, new Vector3(point.x, building.TopY + PreviewYOffset, point.y));
            marker.RegisterRenderer(edge);
        }

        marker.SetVisualizationVisible(_visualsVisible);
        marker.SetHighlighted(_highlightedBuildingId == building.Id);
        _markers[building.Id] = marker;
    }

    private void EnsurePendingPreviewVisuals()
    {
        if (_pendingRoot != null)
        {
            return;
        }

        _pendingRoot = new GameObject("Wyrdrasil_PendingBuildingAuthoring");
        _pendingFootprintLine = CreatePreviewLineRenderer(_pendingRoot, "PendingBuildingFootprint", false, 0.08f, new Color(0.15f, 0.65f, 1f, 1f));
        _pendingBaseLine = CreatePreviewLineRenderer(_pendingRoot, "PendingBuildingBase", true, 0.06f, new Color(0.1f, 0.75f, 1f, 1f));
        _pendingTopLine = CreatePreviewLineRenderer(_pendingRoot, "PendingBuildingTop", true, 0.06f, new Color(0.25f, 0.95f, 1f, 1f));
    }

    private void DestroyPendingPreviewVisuals()
    {
        foreach (var pointVisual in _pendingPointVisuals)
        {
            if (pointVisual != null)
            {
                Object.Destroy(pointVisual);
            }
        }

        _pendingPointVisuals.Clear();
        _pendingHeightEdges.Clear();
        _pendingFootprintLine = null;
        _pendingBaseLine = null;
        _pendingTopLine = null;
        if (_pendingRoot != null)
        {
            Object.Destroy(_pendingRoot);
            _pendingRoot = null;
        }
    }

    private void UpdatePendingPreviewVisuals()
    {
        EnsurePendingPreviewVisuals();
        UpdatePendingPointVisuals();
        if (AuthoringPhase == ZoneAuthoringPhase.Footprint)
        {
            UpdateFootprintPreviewLine();
            SetLineRendererActive(_pendingBaseLine, false);
            SetLineRendererActive(_pendingTopLine, false);
            UpdateHeightEdgePreview(false);
            return;
        }

        if (AuthoringPhase == ZoneAuthoringPhase.Height)
        {
            UpdateHeightPreviewLines();
            return;
        }

        SetLineRendererActive(_pendingFootprintLine, false);
        SetLineRendererActive(_pendingBaseLine, false);
        SetLineRendererActive(_pendingTopLine, false);
        UpdateHeightEdgePreview(false);
    }

    private void UpdatePendingPointVisuals()
    {
        while (_pendingPointVisuals.Count < _pendingFootprintPoints.Count)
        {
            var pointObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pointObject.transform.SetParent(_pendingRoot!.transform, false);
            pointObject.transform.localScale = Vector3.one * 0.22f;
            var collider = pointObject.GetComponent<Collider>();
            if (collider != null)
            {
                Object.Destroy(collider);
            }

            var renderer = pointObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = CreatePreviewMaterial(new Color(0.15f, 0.65f, 1f, 1f));
            }

            _pendingPointVisuals.Add(pointObject);
        }

        for (var i = 0; i < _pendingPointVisuals.Count; i++)
        {
            var active = i < _pendingFootprintPoints.Count && _visualsVisible;
            _pendingPointVisuals[i].SetActive(active);
            if (active)
            {
                _pendingPointVisuals[i].transform.position = _pendingFootprintPoints[i] + Vector3.up * PreviewYOffset;
            }
        }
    }

    private void UpdateFootprintPreviewLine()
    {
        if (_pendingFootprintLine == null)
        {
            return;
        }

        var previewPoints = new List<Vector3>(_pendingFootprintPoints.Select(point => point + Vector3.up * PreviewYOffset));
        if (_pendingGhostPoint.HasValue)
        {
            previewPoints.Add(_pendingGhostPoint.Value + Vector3.up * PreviewYOffset);
        }

        if (previewPoints.Count < 2)
        {
            _pendingFootprintLine.enabled = false;
            return;
        }

        _pendingFootprintLine.loop = false;
        _pendingFootprintLine.positionCount = previewPoints.Count;
        for (var i = 0; i < previewPoints.Count; i++)
        {
            _pendingFootprintLine.SetPosition(i, previewPoints[i]);
        }

        _pendingFootprintLine.enabled = _visualsVisible;
    }

    private void UpdateHeightPreviewLines()
    {
        if (_pendingBaseLine == null || _pendingTopLine == null)
        {
            return;
        }

        var basePoints = _pendingFootprintPoints.Select(point => new Vector3(point.x, _pendingBaseY + PreviewYOffset, point.z)).ToArray();
        var topPoints = _pendingFootprintPoints.Select(point => new Vector3(point.x, _pendingTopY + PreviewYOffset, point.z)).ToArray();
        ApplyClosedLine(_pendingBaseLine, basePoints);
        ApplyClosedLine(_pendingTopLine, topPoints);
        UpdateHeightEdgePreview(true);
        if (_pendingFootprintLine != null)
        {
            _pendingFootprintLine.enabled = false;
        }
    }

    private void UpdateHeightEdgePreview(bool visible)
    {
        while (_pendingHeightEdges.Count < _pendingFootprintPoints.Count)
        {
            var edgeObject = new GameObject();
            edgeObject.transform.SetParent(_pendingRoot!.transform, false);
            var edge = edgeObject.AddComponent<LineRenderer>();
            edge.useWorldSpace = true;
            edge.positionCount = 2;
            edge.startWidth = 0.035f;
            edge.endWidth = 0.035f;
            edge.material = CreatePreviewMaterial(new Color(0.25f, 0.9f, 1f, 1f));
            edge.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            edge.receiveShadows = false;
            _pendingHeightEdges.Add(edge);
        }

        for (var i = 0; i < _pendingHeightEdges.Count; i++)
        {
            var active = visible && i < _pendingFootprintPoints.Count && _visualsVisible;
            _pendingHeightEdges[i].enabled = active;
            if (!active)
            {
                continue;
            }

            var point = _pendingFootprintPoints[i];
            _pendingHeightEdges[i].SetPosition(0, new Vector3(point.x, _pendingBaseY + PreviewYOffset, point.z));
            _pendingHeightEdges[i].SetPosition(1, new Vector3(point.x, _pendingTopY + PreviewYOffset, point.z));
        }
    }

    private void SetHighlightedBuilding(int? buildingId)
    {
        if (_highlightedBuildingId == buildingId)
        {
            return;
        }

        if (_highlightedBuildingId.HasValue && _markers.TryGetValue(_highlightedBuildingId.Value, out var previousMarker))
        {
            previousMarker.SetHighlighted(false);
        }

        _highlightedBuildingId = buildingId;
        if (_highlightedBuildingId.HasValue && _markers.TryGetValue(_highlightedBuildingId.Value, out var nextMarker))
        {
            nextMarker.SetHighlighted(true);
        }
    }

    private static LineRenderer CreatePreviewLineRenderer(GameObject root, string name, bool loop, float width, Color color)
    {
        var child = new GameObject(name);
        child.transform.SetParent(root.transform, false);
        return CreateWorldLineRenderer(child, loop, width, color);
    }

    private static LineRenderer CreateWorldLineRenderer(GameObject owner, bool loop, float width, Color color)
    {
        var line = owner.AddComponent<LineRenderer>();
        line.loop = loop;
        line.useWorldSpace = true;
        line.positionCount = 0;
        line.startWidth = width;
        line.endWidth = width;
        line.material = CreatePreviewMaterial(color);
        line.startColor = color;
        line.endColor = color;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.enabled = false;
        return line;
    }

    private static Material CreatePreviewMaterial(Color color)
    {
        var shader = Shader.Find("Sprites/Default");
        if (!shader)
        {
            shader = Shader.Find("Unlit/Color");
        }

        return new Material(shader) { color = color };
    }

    private static void ApplyClosedLine(LineRenderer renderer, IReadOnlyList<Vector3> points)
    {
        renderer.loop = true;
        renderer.positionCount = points.Count;
        for (var i = 0; i < points.Count; i++)
        {
            renderer.SetPosition(i, points[i]);
        }

        renderer.enabled = true;
    }

    private static void SetLineRendererActive(LineRenderer? lineRenderer, bool isVisible)
    {
        if (lineRenderer != null)
        {
            lineRenderer.enabled = isVisible;
        }
    }

    private static Vector3 ComputeAnchorPosition(IReadOnlyList<Vector3> points) =>
        new(points.Average(point => point.x), points.Average(point => point.y), points.Average(point => point.z));

    private static float SnapHeight(float value) => Mathf.Round(value / VerticalSnapStep) * VerticalSnapStep;
    private static bool IsValidSupportSurface(Vector3 normal) => Vector3.Dot(normal.normalized, Vector3.up) >= SurfaceUpDotThreshold;
    private static Vector2 ToPoint2D(Vector3 point) => new(point.x, point.z);

    private static bool DoSegmentsIntersect(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
    {
        var o1 = Orientation(a1, a2, b1);
        var o2 = Orientation(a1, a2, b2);
        var o3 = Orientation(b1, b2, a1);
        var o4 = Orientation(b1, b2, a2);
        return o1 != o2 && o3 != o4;
    }

    private static int Orientation(Vector2 a, Vector2 b, Vector2 c)
    {
        var value = ((b.y - a.y) * (c.x - b.x)) - ((b.x - a.x) * (c.y - b.y));
        if (Mathf.Abs(value) < 0.0001f)
        {
            return 0;
        }

        return value > 0f ? 1 : 2;
    }
}
