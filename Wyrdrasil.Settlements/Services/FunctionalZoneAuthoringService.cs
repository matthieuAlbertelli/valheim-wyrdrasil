using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using UnityEngine;
using Wyrdrasil.Settlements.Tool;

namespace Wyrdrasil.Settlements.Services;

public sealed class FunctionalZoneAuthoringService
{
    private const float DefaultZoneHeight = 4f;
    private const float VerticalSnapStep = 0.5f;
    private const float CloseFootprintDistance = 1f;
    private const float MinPointSpacing = 0.75f;
    private const float MinZoneHeight = 1f;
    private const float PreviewYOffset = 0.05f;

    private readonly ManualLogSource _log;
    private readonly FunctionalZonePlacementProbe _placementProbe;
    private readonly BuildingService _buildingService;

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
    private bool _visualsVisible;

    public FunctionalZoneAuthoringService(
        ManualLogSource log,
        FunctionalZonePlacementProbe placementProbe,
        BuildingService buildingService,
        bool visualsVisible)
    {
        _log = log;
        _placementProbe = placementProbe;
        _buildingService = buildingService;
        _visualsVisible = visualsVisible;
    }

    public bool IsZoneAuthoringActive => AuthoringPhase != ZoneAuthoringPhase.None;
    public bool IsZoneHeightEditingActive => AuthoringPhase == ZoneAuthoringPhase.Height;
    public ZoneAuthoringPhase AuthoringPhase { get; private set; }

    public PendingZoneAuthoringSnapshot? GetPendingZoneAuthoringSnapshot()
    {
        if (!IsZoneAuthoringActive)
        {
            return null;
        }

        return new PendingZoneAuthoringSnapshot(AuthoringPhase, _pendingFootprintPoints.Count, _pendingBaseY, _pendingTopY, CanClosePendingFootprint());
    }

    public void CreateTavernZone(int nextZoneId, ICollection<FunctionalZoneData> zones, System.Action<FunctionalZoneData> onZoneCreated) =>
        HandleZoneAuthoringPrimaryInput(ZoneType.Tavern, nextZoneId, zones, onZoneCreated);

    public void CreateBedroomZone(int nextZoneId, ICollection<FunctionalZoneData> zones, System.Action<FunctionalZoneData> onZoneCreated) =>
        HandleZoneAuthoringPrimaryInput(ZoneType.Bedroom, nextZoneId, zones, onZoneCreated);

    public void UpdatePendingZoneAuthoringPreview()
    {
        if (AuthoringPhase != ZoneAuthoringPhase.Footprint)
        {
            return;
        }

        _pendingGhostPoint = _placementProbe.TryGetPlacementPoint(out var placementPoint)
            ? CanClosePendingFootprint(placementPoint) ? _pendingFootprintPoints[0] : placementPoint
            : null;

        UpdatePendingPreviewVisuals();
    }

    public void HandleZoneAuthoringPrimaryInput(ZoneType zoneType, int nextZoneId, ICollection<FunctionalZoneData> zones, System.Action<FunctionalZoneData> onZoneCreated)
    {
        if (AuthoringPhase == ZoneAuthoringPhase.Height)
        {
            var finalizedZone = FinalizePendingZone(nextZoneId);
            if (finalizedZone != null)
            {
                zones.Add(finalizedZone);
                onZoneCreated(finalizedZone);
            }

            return;
        }

        if (!_placementProbe.TryGetPlacementPoint(out var placementPoint))
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

        if (CanClosePendingFootprint(placementPoint))
        {
            BeginZoneHeightEditing();
            return;
        }

        TryAddPendingFootprintPoint(placementPoint);
    }

    public void HandleZoneAuthoringSecondaryInput()
    {
        if (AuthoringPhase == ZoneAuthoringPhase.None)
        {
            return;
        }

        if (AuthoringPhase == ZoneAuthoringPhase.Height)
        {
            CancelPendingZoneAuthoring();
            return;
        }

        if (_pendingFootprintPoints.Count > 0)
        {
            _pendingFootprintPoints.RemoveAt(_pendingFootprintPoints.Count - 1);
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

    public void SetVisualizationVisible(bool isVisible)
    {
        _visualsVisible = isVisible;
        UpdatePendingPreviewVisuals();
        foreach (var pointVisual in _pendingPointVisuals)
        {
            if (pointVisual != null)
            {
                pointVisual.SetActive(isVisible && AuthoringPhase != ZoneAuthoringPhase.None);
            }
        }

        if (!isVisible)
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
    }

    private void BeginZoneHeightEditing()
    {
        var minY = _pendingFootprintPoints.Min(point => point.y);
        _pendingBaseY = SnapHeight(minY);
        _pendingTopY = SnapHeight(_pendingBaseY + DefaultZoneHeight);
        AuthoringPhase = ZoneAuthoringPhase.Height;
        _pendingGhostPoint = null;
        UpdatePendingPreviewVisuals();
    }

    private FunctionalZoneData? FinalizePendingZone(int nextZoneId)
    {
        if (_pendingZoneType == null || _pendingFootprintPoints.Count < 3)
        {
            return null;
        }

        var anchorPosition = ComputeAnchorPosition(_pendingFootprintPoints);
        var building = _buildingService.FindBuildingContainingPoint(anchorPosition) ??
                       _buildingService.CreateImplicitBuildingForZone(_pendingZoneType.Value, anchorPosition);
        var zoneData = new FunctionalZoneData(
            nextZoneId,
            building.Id,
            _pendingZoneType.Value,
            anchorPosition,
            _pendingFootprintPoints.Select(point => new Vector2(point.x, point.z)),
            _pendingBaseY,
            _pendingTopY,
            0);

        CancelPendingZoneAuthoring();
        return zoneData;
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

        var previewPoints = new List<Vector3>(_pendingFootprintPoints.Select(p => p + Vector3.up * PreviewYOffset));
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

        var basePoints = _pendingFootprintPoints.Select(p => new Vector3(p.x, _pendingBaseY + PreviewYOffset, p.z)).ToArray();
        var topPoints = _pendingFootprintPoints.Select(p => new Vector3(p.x, _pendingTopY + PreviewYOffset, p.z)).ToArray();
        ApplyClosedLine(_pendingBaseLine, basePoints, _visualsVisible);
        ApplyClosedLine(_pendingTopLine, topPoints, _visualsVisible);
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
            edge.startWidth = 0.03f;
            edge.endWidth = 0.03f;
            edge.material = CreatePreviewMaterial(new Color(0.4f, 0.9f, 1f, 1f));
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

    private static LineRenderer CreatePreviewLineRenderer(GameObject root, string name, bool loop, float width, Color color)
    {
        var child = new GameObject(name);
        child.transform.SetParent(root.transform, false);
        var line = child.AddComponent<LineRenderer>();
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

        var material = new Material(shader)
        {
            color = color
        };

        return material;
    }

    private static void ApplyClosedLine(LineRenderer renderer, IReadOnlyList<Vector3> points, bool isVisible)
    {
        renderer.loop = true;
        renderer.positionCount = points.Count;
        for (var i = 0; i < points.Count; i++)
        {
            renderer.SetPosition(i, points[i]);
        }

        renderer.enabled = isVisible;
    }

    private static void SetLineRendererActive(LineRenderer? lineRenderer, bool isVisible)
    {
        if (lineRenderer != null)
        {
            lineRenderer.enabled = isVisible;
        }
    }

    private static Vector3 ComputeAnchorPosition(IReadOnlyList<Vector3> points) =>
        new(points.Average(p => p.x), points.Average(p => p.y), points.Average(p => p.z));

    private static float SnapHeight(float value) => Mathf.Round(value / VerticalSnapStep) * VerticalSnapStep;
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
