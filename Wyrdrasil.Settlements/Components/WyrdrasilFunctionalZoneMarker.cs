using System.Collections.Generic;
using UnityEngine;
using Wyrdrasil.Settlements.Tool;

namespace Wyrdrasil.Settlements.Components;

public sealed class WyrdrasilFunctionalZoneMarker : MonoBehaviour
{
    private sealed class RendererBinding
    {
        public Renderer Renderer { get; }
        public Color BaseColor { get; }

        public RendererBinding(Renderer renderer, Color baseColor)
        {
            Renderer = renderer;
            BaseColor = baseColor;
        }
    }

    private static readonly Color HighlightColor = new(0.2f, 1f, 0.35f, 1f);

    private readonly List<RendererBinding> _rendererBindings = new();
    private bool _isHighlighted;

    public int ZoneId { get; private set; }

    public int BuildingId { get; private set; }

    public ZoneType ZoneType { get; private set; }

    public float BaseY { get; private set; }

    public float TopY { get; private set; }

    public int FootprintPointCount { get; private set; }

    public void Initialize(int zoneId, int buildingId, ZoneType zoneType, float baseY, float topY, int footprintPointCount)
    {
        ZoneId = zoneId;
        BuildingId = buildingId;
        ZoneType = zoneType;
        BaseY = baseY;
        TopY = topY;
        FootprintPointCount = footprintPointCount;
    }

    public void RegisterRenderer(Renderer? renderer)
    {
        if (renderer == null)
        {
            return;
        }

        var baseColor = ExtractCurrentColor(renderer);
        _rendererBindings.Add(new RendererBinding(renderer, baseColor));
        ApplyRendererColor(renderer, _isHighlighted ? HighlightColor : baseColor);
    }

    public void SetVisualizationVisible(bool isVisible)
    {
        foreach (var rendererBinding in _rendererBindings)
        {
            if (rendererBinding.Renderer != null)
            {
                rendererBinding.Renderer.enabled = isVisible;
            }
        }
    }

    public void SetHighlighted(bool isHighlighted)
    {
        if (_isHighlighted == isHighlighted)
        {
            return;
        }

        _isHighlighted = isHighlighted;
        foreach (var rendererBinding in _rendererBindings)
        {
            if (rendererBinding.Renderer == null)
            {
                continue;
            }

            ApplyRendererColor(
                rendererBinding.Renderer,
                isHighlighted ? HighlightColor : rendererBinding.BaseColor);
        }
    }

    private static Color ExtractCurrentColor(Renderer renderer)
    {
        if (renderer is LineRenderer lineRenderer)
        {
            return lineRenderer.startColor;
        }

        if (renderer.sharedMaterial != null)
        {
            return renderer.sharedMaterial.color;
        }

        return Color.white;
    }

    private static void ApplyRendererColor(Renderer renderer, Color color)
    {
        if (renderer is LineRenderer lineRenderer)
        {
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;
        }

        if (renderer.material != null)
        {
            renderer.material.color = color;
        }
    }
}
