using System.Collections.Generic;
using UnityEngine;

namespace Wyrdrasil.Settlements.Components;


public sealed class WyrdrasilNavigationWaypointMarker : MonoBehaviour
{
    private readonly List<Renderer> _renderers = new();
    private readonly List<Collider> _colliders = new();

    public int WaypointId { get; private set; }

    private void Awake()
    {
        foreach (var collider in GetComponentsInChildren<Collider>(true))
        {
            RegisterCollider(collider);
        }
    }

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

        if (!_renderers.Contains(renderer))
        {
            _renderers.Add(renderer);
        }

        foreach (var collider in renderer.GetComponentsInChildren<Collider>(true))
        {
            RegisterCollider(collider);
        }

        var sameObjectCollider = renderer.GetComponent<Collider>();
        if (sameObjectCollider != null)
        {
            RegisterCollider(sameObjectCollider);
        }
    }

    public void RegisterCollider(Collider? collider)
    {
        if (collider == null)
        {
            return;
        }

        if (!_colliders.Contains(collider))
        {
            _colliders.Add(collider);
        }

        collider.isTrigger = true;
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
