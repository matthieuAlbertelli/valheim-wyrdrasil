using System.Collections.Generic;
using UnityEngine;

namespace Wyrdrasil.Settlements.Components;

public sealed class WyrdrasilBuildingMarker : MonoBehaviour
{
    private sealed class RendererBinding
    {
        public Renderer Renderer { get; }
        public Color BaseColor { get; }
        public float BaseWidthMultiplier { get; }

        public RendererBinding(Renderer renderer, Color baseColor, float baseWidthMultiplier)
        {
            Renderer = renderer;
            BaseColor = baseColor;
            BaseWidthMultiplier = baseWidthMultiplier;
        }
    }

    private static readonly Color HighlightColor = new(0.25f, 0.85f, 1f, 1f);

    private readonly List<RendererBinding> _rendererBindings = new();
    private bool _isHighlighted;

    public int BuildingId { get; private set; }
    public float BaseY { get; private set; }
    public float TopY { get; private set; }
    public int FootprintPointCount { get; private set; }

    public void Initialize(int buildingId, float baseY, float topY, int footprintPointCount)
    {
        BuildingId = buildingId;
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
        var baseWidthMultiplier = ExtractCurrentWidthMultiplier(renderer);
        _rendererBindings.Add(new RendererBinding(renderer, baseColor, baseWidthMultiplier));
        ApplyRendererVisual(renderer, _isHighlighted ? HighlightColor : baseColor, _isHighlighted, baseWidthMultiplier);
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

            ApplyRendererVisual(
                rendererBinding.Renderer,
                isHighlighted ? HighlightColor : rendererBinding.BaseColor,
                isHighlighted,
                rendererBinding.BaseWidthMultiplier);
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

    private static float ExtractCurrentWidthMultiplier(Renderer renderer)
    {
        return renderer is LineRenderer lineRenderer
            ? Mathf.Max(0.01f, lineRenderer.widthMultiplier)
            : 1f;
    }

    private static void ApplyRendererVisual(Renderer renderer, Color color, bool highlighted, float baseWidthMultiplier)
    {
        if (renderer is LineRenderer lineRenderer)
        {
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;
            lineRenderer.widthMultiplier = highlighted
                ? Mathf.Max(baseWidthMultiplier * 2.4f, baseWidthMultiplier + 0.08f)
                : baseWidthMultiplier;
        }

        if (renderer.material != null)
        {
            renderer.material.color = color;
        }
    }
}
