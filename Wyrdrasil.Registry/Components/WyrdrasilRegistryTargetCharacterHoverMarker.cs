using UnityEngine;

namespace Wyrdrasil.Registry.Components;

public sealed class WyrdrasilRegistryTargetCharacterHoverMarker : MonoBehaviour
{
    private const int RingSegments = 36;
    private const float RingRadius = 0.82f;
    private const float RingHeight = 0.13f;
    private const float RingWidth = 0.075f;

    private static readonly Color RegisteredColor = new(0.25f, 1f, 0.25f, 1f);
    private static readonly Color UnregisteredColor = new(1f, 0.18f, 0.12f, 1f);

    private LineRenderer? _ringRenderer;
    private bool _isVisible;
    private bool _isRegistered;

    public void SetState(bool isVisible, bool isRegistered)
    {
        _isVisible = isVisible;
        _isRegistered = isRegistered;
        EnsureVisual();
        RefreshVisual();
    }

    public void Hide()
    {
        _isVisible = false;
        RefreshVisual();
    }

    private void EnsureVisual()
    {
        if (_ringRenderer != null)
        {
            return;
        }

        var ringObject = new GameObject("WyrdrasilRegistryTargetHoverRing");
        ringObject.transform.SetParent(transform, false);
        ringObject.transform.localPosition = new Vector3(0f, RingHeight, 0f);

        _ringRenderer = ringObject.AddComponent<LineRenderer>();
        _ringRenderer.loop = true;
        _ringRenderer.useWorldSpace = false;
        _ringRenderer.positionCount = RingSegments;
        _ringRenderer.startWidth = RingWidth;
        _ringRenderer.endWidth = RingWidth;
        _ringRenderer.material = CreateLineMaterial();
        _ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _ringRenderer.receiveShadows = false;

        for (var i = 0; i < RingSegments; i++)
        {
            var angle = i / (float)RingSegments * Mathf.PI * 2f;
            _ringRenderer.SetPosition(i, new Vector3(
                Mathf.Cos(angle) * RingRadius,
                0f,
                Mathf.Sin(angle) * RingRadius));
        }
    }

    private void RefreshVisual()
    {
        if (_ringRenderer == null)
        {
            return;
        }

        _ringRenderer.enabled = _isVisible;
        var color = _isRegistered ? RegisteredColor : UnregisteredColor;
        _ringRenderer.startColor = color;
        _ringRenderer.endColor = color;
    }

    private static Material CreateLineMaterial()
    {
        var shader = Shader.Find("Sprites/Default");
        if (!shader)
        {
            shader = Shader.Find("Unlit/Color");
        }

        return new Material(shader);
    }
}
