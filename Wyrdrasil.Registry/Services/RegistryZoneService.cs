using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using UnityEngine;
using Wyrdrasil.Registry.Components;
using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Services;

public sealed class RegistryZoneService
{
    private const float DefaultZoneHeight = 4f;
    private const float VerticalSnapStep = 0.5f;
    private const float SurfaceUpDotThreshold = 0.55f;
    private const float CloseFootprintDistance = 1f;
    private const float MinPointSpacing = 0.75f;
    private const float MinZoneHeight = 1f;
    private const float PreviewYOffset = 0.05f;

    private readonly ManualLogSource _log;
    private readonly RegistryBuildingService _buildingService;
    private readonly List<FunctionalZoneData> _zones = new();
    private readonly Dictionary<int, WyrdrasilFunctionalZoneMarker> _markers = new();
    private readonly Dictionary<int, GameObject> _zoneRoots = new();
    private readonly List<Vector3> _pendingFootprintPoints = new();
    private readonly List<GameObject> _pendingPointVisuals = new();
    private readonly List<LineRenderer> _pendingHeightEdges = new();

    private GameObject? _pendingRoot;
    private LineRenderer? _pendingFootprintLine;
    private LineRenderer? _pendingBaseLine;
    private LineRenderer? _pendingTopLine;
    private ZoneType? _pendingZoneType;
    private Vector3? _pendingGhostPoint;
    private float _pendingBaseY;
    private float _pendingTopY;
    private int _nextZoneId = 1;
    private bool _visualsVisible;

    public IReadOnlyList<FunctionalZoneData> Zones => _zones;
    public bool IsZoneAuthoringActive => AuthoringPhase != ZoneAuthoringPhase.None;
    public bool IsZoneHeightEditingActive => AuthoringPhase == ZoneAuthoringPhase.Height;
    public ZoneAuthoringPhase AuthoringPhase { get; private set; }

    public RegistryZoneService(ManualLogSource log, RegistryModeService modeService, RegistryBuildingService buildingService)
    {
        _log = log;
        _buildingService = buildingService;
        _visualsVisible = modeService.IsRegistryModeEnabled;
        modeService.RegistryModeChanged += OnRegistryModeChanged;
    }

    public PendingZoneAuthoringSnapshot? GetPendingZoneAuthoringSnapshot()
    {
        if (!IsZoneAuthoringActive)
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

    public void CreateTavernZone()
    {
        HandleZoneAuthoringPrimaryInput(ZoneType.Tavern);
    }

    public void UpdatePendingZoneAuthoringPreview()
    {
        if (AuthoringPhase != ZoneAuthoringPhase.Footprint)
        {
            return;
        }

        if (TryGetPlacementPoint(out var placementPoint))
        {
            _pendingGhostPoint = CanClosePendingFootprint(placementPoint)
                ? _pendingFootprintPoints[0]
                : placementPoint;
        }
        else
        {
            _pendingGhostPoint = null;
        }

        UpdatePendingPreviewVisuals();
    }

    public void HandleZoneAuthoringPrimaryInput(ZoneType zoneType)
    {
        if (AuthoringPhase == ZoneAuthoringPhase.Height)
        {
            FinalizePendingZone();
            return;
        }

        if (!TryGetPlacementPoint(out var placementPoint))
        {
            _log.LogWarning($"Cannot place point for zone '{zoneType}': no valid support surface was found.");
            return;
        }

        if (AuthoringPhase == ZoneAuthoringPhase.None)
        {
            BeginZoneAuthoring(zoneType);
        }

        if (_pendingZoneType != zoneType)
        {
            CancelPendingZoneAuthoring();
            BeginZoneAuthoring(zoneType);
        }

        if (AuthoringPhase == ZoneAuthoringPhase.Footprint)
        {
            if (CanClosePendingFootprint(placementPoint))
            {
                BeginZoneHeightEditing();
                return;
            }

            TryAddPendingFootprintPoint(placementPoint);
        }
    }

    public void HandleZoneAuthoringSecondaryInput()
    {
        if (AuthoringPhase == ZoneAuthoringPhase.None)
        {
            return;
        }

        if (AuthoringPhase == ZoneAuthoringPhase.Height)
        {
            _log.LogInfo("Canceled pending zone height editing.");
            CancelPendingZoneAuthoring();
            return;
        }

        if (_pendingFootprintPoints.Count > 0)
        {
            _pendingFootprintPoints.RemoveAt(_pendingFootprintPoints.Count - 1);
            _log.LogInfo($"Removed last pending zone point. Remaining points: {_pendingFootprintPoints.Count}.");

            if (_pendingFootprintPoints.Count == 0)
            {
                CancelPendingZoneAuthoring();
                return;
            }

            UpdatePendingPreviewVisuals();
        }
    }

    public void AdjustPendingZoneHeight(int direction, bool adjustBase)
    {
        if (AuthoringPhase != ZoneAuthoringPhase.Height || direction == 0)
        {
            return;
        }

        var delta = direction > 0 ? VerticalSnapStep : -VerticalSnapStep;
        if (adjustBase)
        {
            var nextBase = SnapHeight(_pendingBaseY + delta);
            if (_pendingTopY - nextBase < MinZoneHeight)
            {
                nextBase = _pendingTopY - MinZoneHeight;
            }

            _pendingBaseY = SnapHeight(nextBase);
        }
        else
        {
            var nextTop = SnapHeight(_pendingTopY + delta);
            if (nextTop - _pendingBaseY < MinZoneHeight)
            {
                nextTop = _pendingBaseY + MinZoneHeight;
            }

            _pendingTopY = SnapHeight(nextTop);
        }

        UpdatePendingPreviewVisuals();
    }

    public void CancelPendingZoneAuthoring()
    {
        _pendingFootprintPoints.Clear();
        _pendingGhostPoint = null;
        _pendingZoneType = null;
        _pendingBaseY = 0f;
        _pendingTopY = 0f;
        AuthoringPhase = ZoneAuthoringPhase.None;
        DestroyPendingPreviewVisuals();
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

    public bool TryFindZoneAtPoint(Vector3 point, out FunctionalZoneData zone)
    {
        var match = FindZoneContainingPoint(point);
        if (match != null)
        {
            zone = match;
            return true;
        }

        zone = null!;
        return false;
    }

    public bool DeleteZone(int zoneId, out FunctionalZoneData? deletedZone)
    {
        var index = _zones.FindIndex(zone => zone.Id == zoneId);
        if (index < 0)
        {
            deletedZone = null;
            return false;
        }

        deletedZone = _zones[index];
        _zones.RemoveAt(index);

        if (_zoneRoots.TryGetValue(zoneId, out var root) && root != null)
        {
            Object.Destroy(root);
        }

        _zoneRoots.Remove(zoneId);
        _markers.Remove(zoneId);
        return true;
    }

    public FunctionalZoneData? FindZoneContainingPoint(Vector3 point)
    {
        FunctionalZoneData? bestMatch = null;
        var bestDistance = float.MaxValue;

        foreach (var zone in _zones)
        {
            if (!zone.ContainsPoint(point))
            {
                continue;
            }

            var distance = Vector2.Distance(new Vector2(zone.Position.x, zone.Position.z), new Vector2(point.x, point.z));
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestMatch = zone;
        }

        return bestMatch;
    }

    public FunctionalZoneData? FindZoneContainingPoint(Vector3 point, ZoneType zoneType)
    {
        return _zones
            .Where(zone => zone.ZoneType == zoneType && zone.ContainsPoint(point))
            .OrderBy(zone => Vector2.Distance(new Vector2(zone.Position.x, zone.Position.z), new Vector2(point.x, point.z)))
            .FirstOrDefault();
    }

    public FunctionalZoneData? FindZoneContainingPointHorizontally(Vector3 point)
    {
        FunctionalZoneData? bestMatch = null;
        var bestDistance = float.MaxValue;

        foreach (var zone in _zones)
        {
            if (!zone.ContainsPointHorizontally(point))
            {
                continue;
            }

            var distance = Vector2.Distance(new Vector2(zone.Position.x, zone.Position.z), new Vector2(point.x, point.z));
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestMatch = zone;
        }

        return bestMatch;
    }

    public FunctionalZoneData? FindZoneContainingPointHorizontally(Vector3 point, ZoneType zoneType)
    {
        return _zones
            .Where(zone => zone.ZoneType == zoneType && zone.ContainsPointHorizontally(point))
            .OrderBy(zone => Vector2.Distance(new Vector2(zone.Position.x, zone.Position.z), new Vector2(point.x, point.z)))
            .FirstOrDefault();
    }

    private void OnRegistryModeChanged(bool isEnabled)
    {
        _visualsVisible = isEnabled;
        foreach (var marker in _markers.Values)
        {
            marker.SetVisualizationVisible(isEnabled);
        }

        SetPendingPreviewVisible(isEnabled);
        if (!isEnabled)
        {
            CancelPendingZoneAuthoring();
        }
    }

    private void BeginZoneAuthoring(ZoneType zoneType)
    {
        _pendingZoneType = zoneType;
        AuthoringPhase = ZoneAuthoringPhase.Footprint;
        EnsurePendingPreviewVisuals();
        UpdatePendingPreviewVisuals();
        _log.LogInfo($"Started {zoneType} zone authoring. Left click to place points.");
    }

    private void BeginZoneHeightEditing()
    {
        if (_pendingFootprintPoints.Count < 3 || _pendingZoneType == null)
        {
            return;
        }

        var minY = _pendingFootprintPoints.Min(point => point.y);
        _pendingBaseY = SnapHeight(minY);
        _pendingTopY = SnapHeight(_pendingBaseY + DefaultZoneHeight);
        AuthoringPhase = ZoneAuthoringPhase.Height;
        _pendingGhostPoint = null;
        UpdatePendingPreviewVisuals();
        _log.LogInfo($"Closed {_pendingZoneType} zone footprint with {_pendingFootprintPoints.Count} points. Mouse wheel adjusts TopY, Shift + Mouse wheel adjusts BaseY, left click confirms.");
    }

    private void FinalizePendingZone()
    {
        if (_pendingZoneType == null || _pendingFootprintPoints.Count < 3)
        {
            return;
        }

        var anchorPosition = ComputeAnchorPosition(_pendingFootprintPoints);
        var building = _buildingService.CreateImplicitBuildingForZone(_pendingZoneType.Value, anchorPosition);
        var zoneData = new FunctionalZoneData(
            _nextZoneId++,
            building.Id,
            _pendingZoneType.Value,
            anchorPosition,
            _pendingFootprintPoints.Select(point => new Vector2(point.x, point.z)),
            _pendingBaseY,
            _pendingTopY,
            levelIndex: 0);

        _zones.Add(zoneData);
        CreateZoneWorldObject(zoneData);
        _log.LogInfo($"Created {_pendingZoneType} zone #{zoneData.Id} in building #{zoneData.BuildingId} with {_pendingFootprintPoints.Count} footprint points, BaseY={zoneData.BaseY:0.00}, TopY={zoneData.TopY:0.00}.");
        CancelPendingZoneAuthoring();
    }

    private bool TryAddPendingFootprintPoint(Vector3 point)
    {
        if (_pendingFootprintPoints.Count > 0)
        {
            var previousPoint = _pendingFootprintPoints[_pendingFootprintPoints.Count - 1];
            if (Vector3.Distance(previousPoint, point) < MinPointSpacing)
            {
                _log.LogWarning("Cannot add zone point: it is too close to the previous point.");
                return false;
            }
        }

        if (WouldNewFootprintSegmentSelfIntersect(point))
        {
            _log.LogWarning("Cannot add zone point: the new segment would self-intersect the footprint.");
            return false;
        }

        _pendingFootprintPoints.Add(point);
        UpdatePendingPreviewVisuals();
        _log.LogInfo($"Added pending zone point #{_pendingFootprintPoints.Count} at {point}.");
        return true;
    }

    private bool CanClosePendingFootprint()
    {
        return _pendingFootprintPoints.Count >= 3;
    }

    private bool CanClosePendingFootprint(Vector3 candidatePoint)
    {
        if (_pendingFootprintPoints.Count < 3)
        {
            return false;
        }

        var firstPoint = _pendingFootprintPoints[0];
        var horizontalDistance = Vector2.Distance(new Vector2(candidatePoint.x, candidatePoint.z), new Vector2(firstPoint.x, firstPoint.z));
        if (horizontalDistance > CloseFootprintDistance)
        {
            return false;
        }

        return !WouldClosingSegmentSelfIntersect();
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
            var edgeStart = ToPoint2D(_pendingFootprintPoints[i]);
            var edgeEnd = ToPoint2D(_pendingFootprintPoints[i + 1]);
            if (DoSegmentsIntersect(segmentStart, segmentEnd, edgeStart, edgeEnd))
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
            var edgeStart = ToPoint2D(_pendingFootprintPoints[i]);
            var edgeEnd = ToPoint2D(_pendingFootprintPoints[i + 1]);
            if (DoSegmentsIntersect(segmentStart, segmentEnd, edgeStart, edgeEnd))
            {
                return true;
            }
        }

        return false;
    }

    private void EnsurePendingPreviewVisuals()
    {
        if (_pendingRoot != null)
        {
            return;
        }

        _pendingRoot = new GameObject("Wyrdrasil_PendingZoneAuthoring");

        _pendingFootprintLine = CreatePreviewLineRenderer(_pendingRoot, "PendingFootprint", false, 0.06f, new Color(1f, 0.85f, 0.25f, 1f));
        _pendingBaseLine = CreatePreviewLineRenderer(_pendingRoot, "PendingBase", true, 0.05f, new Color(0.3f, 0.9f, 1f, 1f));
        _pendingTopLine = CreatePreviewLineRenderer(_pendingRoot, "PendingTop", true, 0.05f, new Color(0.3f, 1f, 0.5f, 1f));

        SetPendingPreviewVisible(_visualsVisible);
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
            UpdateHeightEdgePreview(visible: false);
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
        UpdateHeightEdgePreview(visible: false);
    }

    private void UpdatePendingPointVisuals()
    {
        while (_pendingPointVisuals.Count < _pendingFootprintPoints.Count)
        {
            var pointObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pointObject.name = $"PendingZonePoint_{_pendingPointVisuals.Count}";
            pointObject.transform.SetParent(_pendingRoot!.transform, false);
            pointObject.transform.localScale = Vector3.one * 0.18f;

            var collider = pointObject.GetComponent<Collider>();
            if (collider != null)
            {
                Object.Destroy(collider);
            }

            var renderer = pointObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = CreatePreviewMaterial(new Color(1f, 0.85f, 0.25f, 1f));
            }

            _pendingPointVisuals.Add(pointObject);
        }

        for (var i = 0; i < _pendingPointVisuals.Count; i++)
        {
            var pointVisual = _pendingPointVisuals[i];
            if (pointVisual == null)
            {
                continue;
            }

            var isActive = i < _pendingFootprintPoints.Count && _visualsVisible;
            pointVisual.SetActive(isActive);
            if (isActive)
            {
                var point = _pendingFootprintPoints[i];
                pointVisual.transform.position = point + Vector3.up * PreviewYOffset;
            }
        }
    }

    private void UpdateFootprintPreviewLine()
    {
        if (_pendingFootprintLine == null)
        {
            return;
        }

        var previewPoints = new List<Vector3>(_pendingFootprintPoints.Count + 1);
        previewPoints.AddRange(_pendingFootprintPoints.Select(point => point + Vector3.up * PreviewYOffset));
        if (_pendingGhostPoint.HasValue)
        {
            previewPoints.Add(_pendingGhostPoint.Value + Vector3.up * PreviewYOffset);
        }

        if (previewPoints.Count < 2)
        {
            SetLineRendererActive(_pendingFootprintLine, false);
            return;
        }

        _pendingFootprintLine.loop = false;
        _pendingFootprintLine.positionCount = previewPoints.Count;
        for (var i = 0; i < previewPoints.Count; i++)
        {
            _pendingFootprintLine.SetPosition(i, previewPoints[i]);
        }

        SetLineRendererActive(_pendingFootprintLine, true);
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
        SetLineRendererActive(_pendingFootprintLine, false);
        UpdateHeightEdgePreview(visible: true);
    }

    private void UpdateHeightEdgePreview(bool visible)
    {
        while (_pendingHeightEdges.Count < _pendingFootprintPoints.Count)
        {
            var edgeObject = new GameObject($"PendingHeightEdge_{_pendingHeightEdges.Count}");
            edgeObject.transform.SetParent(_pendingRoot!.transform, false);
            var edgeRenderer = edgeObject.AddComponent<LineRenderer>();
            edgeRenderer.useWorldSpace = true;
            edgeRenderer.positionCount = 2;
            edgeRenderer.startWidth = 0.03f;
            edgeRenderer.endWidth = 0.03f;
            edgeRenderer.material = CreatePreviewMaterial(new Color(0.4f, 0.9f, 1f, 1f));
            edgeRenderer.startColor = new Color(0.4f, 0.9f, 1f, 1f);
            edgeRenderer.endColor = new Color(0.4f, 0.9f, 1f, 1f);
            edgeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            edgeRenderer.receiveShadows = false;
            _pendingHeightEdges.Add(edgeRenderer);
        }

        for (var i = 0; i < _pendingHeightEdges.Count; i++)
        {
            var edge = _pendingHeightEdges[i];
            if (edge == null)
            {
                continue;
            }

            var isActive = visible && i < _pendingFootprintPoints.Count && _visualsVisible;
            edge.enabled = isActive;
            if (!isActive)
            {
                continue;
            }

            var point = _pendingFootprintPoints[i];
            edge.SetPosition(0, new Vector3(point.x, _pendingBaseY + PreviewYOffset, point.z));
            edge.SetPosition(1, new Vector3(point.x, _pendingTopY + PreviewYOffset, point.z));
        }
    }

    private void CreateZoneWorldObject(FunctionalZoneData zoneData)
    {
        var root = new GameObject($"Wyrdrasil_FunctionalZone_{zoneData.ZoneType}_{zoneData.Id}");
        root.transform.position = zoneData.Position;
        _zoneRoots[zoneData.Id] = root;

        var marker = root.AddComponent<WyrdrasilFunctionalZoneMarker>();
        marker.Initialize(zoneData.Id, zoneData.BuildingId, zoneData.ZoneType, zoneData.BaseY, zoneData.TopY, zoneData.FootprintPoints.Count);

        var outlineObject = new GameObject("ZoneOutline");
        outlineObject.transform.SetParent(root.transform, false);

        var lineRenderer = outlineObject.AddComponent<LineRenderer>();
        lineRenderer.loop = true;
        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount = zoneData.FootprintPoints.Count;
        lineRenderer.startWidth = 0.08f;
        lineRenderer.endWidth = 0.08f;
        lineRenderer.material = CreatePreviewMaterial(new Color(0.95f, 0.7f, 0.2f, 1f));
        lineRenderer.startColor = new Color(0.95f, 0.7f, 0.2f, 1f);
        lineRenderer.endColor = new Color(0.95f, 0.7f, 0.2f, 1f);
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;

        for (var i = 0; i < zoneData.FootprintPoints.Count; i++)
        {
            var point = zoneData.FootprintPoints[i];
            lineRenderer.SetPosition(i, new Vector3(point.x, zoneData.Position.y + PreviewYOffset, point.y));
        }

        marker.RegisterRenderer(lineRenderer);
        marker.SetVisualizationVisible(_visualsVisible);
        _markers[zoneData.Id] = marker;
    }

    private static LineRenderer CreatePreviewLineRenderer(GameObject root, string name, bool loop, float width, Color color)
    {
        var child = new GameObject(name);
        child.transform.SetParent(root.transform, false);

        var lineRenderer = child.AddComponent<LineRenderer>();
        lineRenderer.loop = loop;
        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount = 0;
        lineRenderer.startWidth = width;
        lineRenderer.endWidth = width;
        lineRenderer.material = CreatePreviewMaterial(color);
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.enabled = false;
        return lineRenderer;
    }

    private static Material CreatePreviewMaterial(Color color)
    {
        var shader = Shader.Find("Sprites/Default");
        if (!shader)
        {
            shader = Shader.Find("Unlit/Color");
        }

        var material = new Material(shader)
        {
            color = color
        };

        return material;
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

    private void SetPendingPreviewVisible(bool isVisible)
    {
        if (_pendingRoot == null)
        {
            return;
        }

        if (_pendingFootprintLine != null)
        {
            _pendingFootprintLine.enabled = isVisible && _pendingFootprintLine.positionCount > 1 && AuthoringPhase == ZoneAuthoringPhase.Footprint;
        }

        if (_pendingBaseLine != null)
        {
            _pendingBaseLine.enabled = isVisible && AuthoringPhase == ZoneAuthoringPhase.Height;
        }

        if (_pendingTopLine != null)
        {
            _pendingTopLine.enabled = isVisible && AuthoringPhase == ZoneAuthoringPhase.Height;
        }

        foreach (var pointVisual in _pendingPointVisuals)
        {
            if (pointVisual != null)
            {
                pointVisual.SetActive(isVisible && AuthoringPhase != ZoneAuthoringPhase.None);
            }
        }

        foreach (var edge in _pendingHeightEdges)
        {
            if (edge != null)
            {
                edge.enabled = isVisible && AuthoringPhase == ZoneAuthoringPhase.Height;
            }
        }
    }

    private static void SetLineRendererActive(LineRenderer? lineRenderer, bool isVisible)
    {
        if (lineRenderer != null)
        {
            lineRenderer.enabled = isVisible;
        }
    }

    private static Vector3 ComputeAnchorPosition(IReadOnlyList<Vector3> points)
    {
        var averageX = points.Average(point => point.x);
        var averageY = points.Average(point => point.y);
        var averageZ = points.Average(point => point.z);
        return new Vector3(averageX, averageY, averageZ);
    }

    private static float SnapHeight(float value)
    {
        return Mathf.Round(value / VerticalSnapStep) * VerticalSnapStep;
    }

    private static bool IsValidSupportSurface(Vector3 surfaceNormal)
    {
        return Vector3.Dot(surfaceNormal.normalized, Vector3.up) >= SurfaceUpDotThreshold;
    }

    private static Vector2 ToPoint2D(Vector3 point)
    {
        return new Vector2(point.x, point.z);
    }

    private static bool DoSegmentsIntersect(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
    {
        var o1 = Orientation(a1, a2, b1);
        var o2 = Orientation(a1, a2, b2);
        var o3 = Orientation(b1, b2, a1);
        var o4 = Orientation(b1, b2, a2);

        if (o1 != o2 && o3 != o4)
        {
            return true;
        }

        return false;
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