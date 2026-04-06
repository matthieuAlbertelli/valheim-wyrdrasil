using System.Collections.Generic;
using UnityEngine;

namespace Wyrdrasil.Registry.Components;

public sealed class WyrdrasilNavigationWaypointMarker : MonoBehaviour
{
    private readonly List<Renderer> _renderers = new();

    public int WaypointId { get; private set; }

    public void Initialize(int waypointId)
    {
        WaypointId = waypointId;
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

    public void SetSelected(bool isSelected)
    {
        foreach (var renderer in _renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            renderer.material.color = isSelected
                ? new Color(0.2f, 1f, 0.65f, 1f)
                : new Color(1f, 0.45f, 0.2f, 1f);
        }
    }
}
