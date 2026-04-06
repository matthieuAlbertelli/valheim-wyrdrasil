using System.Collections.Generic;
using UnityEngine;
using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Components;

public sealed class WyrdrasilFunctionalZoneMarker : MonoBehaviour
{
    private readonly List<Renderer> _renderers = new();

    public int ZoneId { get; private set; }

    public ZoneType ZoneType { get; private set; }

    public float Radius { get; private set; }

    public void Initialize(int zoneId, ZoneType zoneType, float radius)
    {
        ZoneId = zoneId;
        ZoneType = zoneType;
        Radius = radius;
    }

    public void RegisterRenderer(Renderer? renderer)
    {
        if (renderer == null)
        {
            return;
        }

        _renderers.Add(renderer);
    }

    public void SetVisualizationVisible(bool isVisible)
    {
        foreach (var renderer in _renderers)
        {
            if (renderer != null)
            {
                renderer.enabled = isVisible;
            }
        }
    }
}
