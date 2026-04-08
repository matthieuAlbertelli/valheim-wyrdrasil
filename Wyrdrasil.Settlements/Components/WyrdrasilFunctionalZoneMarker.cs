using System.Collections.Generic;
using UnityEngine;
using Wyrdrasil.Registry.Tool;
using Wyrdrasil.Settlements.Tool;

namespace Wyrdrasil.Settlements.Components;


public sealed class WyrdrasilFunctionalZoneMarker : MonoBehaviour
{
    private readonly List<Renderer> _renderers = new();

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
