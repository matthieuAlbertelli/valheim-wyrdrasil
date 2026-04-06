using System.Collections.Generic;
using UnityEngine;
using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Components;

public sealed class WyrdrasilZoneSlotMarker : MonoBehaviour
{
    private readonly List<Renderer> _renderers = new();

    public int SlotId { get; private set; }

    public int ZoneId { get; private set; }

    public ZoneSlotType SlotType { get; private set; }

    public void Initialize(int slotId, int zoneId, ZoneSlotType slotType)
    {
        SlotId = slotId;
        ZoneId = zoneId;
        SlotType = slotType;
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

    public void SetOccupied(bool isOccupied)
    {
        foreach (var renderer in _renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            var color = isOccupied
                ? new Color(0.35f, 1f, 0.35f, 1f)
                : SlotType == ZoneSlotType.Innkeeper
                    ? new Color(0.2f, 0.8f, 0.95f, 1f)
                    : new Color(0.95f, 0.95f, 0.35f, 1f);

            renderer.material.color = color;
        }
    }
}
