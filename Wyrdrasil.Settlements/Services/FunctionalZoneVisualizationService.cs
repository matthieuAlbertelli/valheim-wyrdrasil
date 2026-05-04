using System.Collections.Generic;
using UnityEngine;
using Wyrdrasil.Settlements.Components;
using Wyrdrasil.Settlements.Tool;

namespace Wyrdrasil.Settlements.Services;

public sealed class FunctionalZoneVisualizationService
{
    private const float PreviewYOffset = 0.05f;

    private readonly Dictionary<int, WyrdrasilFunctionalZoneMarker> _markers = new();
    private readonly Dictionary<int, GameObject> _zoneRoots = new();

    private int? _highlightedZoneId;
    private bool _visualsVisible;

    public FunctionalZoneVisualizationService(bool visualsVisible)
    {
        _visualsVisible = visualsVisible;
    }

    public void Rebuild(IEnumerable<FunctionalZoneData> zones)
    {
        ClearAll();
        foreach (var zone in zones)
        {
            CreateZoneWorldObject(zone);
        }
    }

    public void AddZone(FunctionalZoneData zoneData)
    {
        CreateZoneWorldObject(zoneData);
    }

    public void ClearAll()
    {
        foreach (var root in _zoneRoots.Values)
        {
            if (root != null)
            {
                Object.Destroy(root);
            }
        }

        _zoneRoots.Clear();
        _markers.Clear();
        _highlightedZoneId = null;
    }

    public void SetVisualizationVisible(bool isVisible)
    {
        _visualsVisible = isVisible;
        foreach (var marker in _markers.Values)
        {
            marker.SetVisualizationVisible(isVisible);
        }

        if (!isVisible)
        {
            SetHighlightedZone(null);
        }
    }

    public void SetHighlightedZone(int? zoneId)
    {
        if (_highlightedZoneId == zoneId)
        {
            return;
        }

        if (_highlightedZoneId.HasValue && _markers.TryGetValue(_highlightedZoneId.Value, out var previousMarker))
        {
            previousMarker.SetHighlighted(false);
        }

        _highlightedZoneId = zoneId;
        if (_highlightedZoneId.HasValue && _markers.TryGetValue(_highlightedZoneId.Value, out var nextMarker))
        {
            nextMarker.SetHighlighted(true);
        }
    }

    public bool RemoveZone(int zoneId)
    {
        if (_zoneRoots.TryGetValue(zoneId, out var root) && root != null)
        {
            Object.Destroy(root);
        }

        _zoneRoots.Remove(zoneId);
        _markers.Remove(zoneId);
        if (_highlightedZoneId == zoneId)
        {
            SetHighlightedZone(null);
        }

        return true;
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
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        for (var i = 0; i < zoneData.FootprintPoints.Count; i++)
        {
            var point = zoneData.FootprintPoints[i];
            lineRenderer.SetPosition(i, new Vector3(point.x, zoneData.Position.y + PreviewYOffset, point.y));
        }

        marker.RegisterRenderer(lineRenderer);
        marker.SetVisualizationVisible(_visualsVisible);
        marker.SetHighlighted(_highlightedZoneId == zoneData.Id);
        _markers[zoneData.Id] = marker;
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
}
