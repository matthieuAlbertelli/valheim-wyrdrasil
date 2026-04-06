using System.Collections.Generic;
using UnityEngine;

namespace Wyrdrasil.Registry.Components;

public sealed class WyrdrasilRegisteredSeatMarker : MonoBehaviour
{
    private readonly List<Renderer> _renderers = new();
    private readonly Dictionary<Renderer, Color> _originalColors = new();

    public int SeatId { get; private set; }

    public void Initialize(int seatId)
    {
        SeatId = seatId;
    }

    public void RegisterRenderers(IEnumerable<Renderer> renderers)
    {
        _renderers.Clear();
        _originalColors.Clear();

        foreach (var renderer in renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            _renderers.Add(renderer);
            if (renderer.material != null)
            {
                _originalColors[renderer] = renderer.material.color;
            }
        }
    }

    public void SetVisualizationVisible(bool isVisible, bool isOccupied)
    {
        foreach (var renderer in _renderers)
        {
            if (renderer == null || renderer.material == null)
            {
                continue;
            }

            if (isVisible)
            {
                renderer.material.color = isOccupied
                    ? new Color(0.35f, 1f, 0.35f, 1f)
                    : new Color(1f, 0.9f, 0.25f, 1f);
            }
            else if (_originalColors.TryGetValue(renderer, out var originalColor))
            {
                renderer.material.color = originalColor;
            }
        }
    }
}
