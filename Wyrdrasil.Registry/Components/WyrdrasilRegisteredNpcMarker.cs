using UnityEngine;
using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Components;

public sealed class WyrdrasilRegisteredNpcMarker : MonoBehaviour
{
    private const int RingSegments = 24;
    private LineRenderer? _lineRenderer;

    public int RegisteredNpcId { get; private set; }

    public string DisplayName { get; private set; } = string.Empty;

    public NpcRole Role { get; private set; }

    public void Initialize(int registeredNpcId, string displayName, NpcRole role)
    {
        RegisteredNpcId = registeredNpcId;
        DisplayName = displayName;
        Role = role;
    }

    public void EnsureVisual()
    {
        if (_lineRenderer != null)
        {
            return;
        }

        var ringObject = new GameObject("WyrdrasilResidentRing");
        ringObject.transform.SetParent(transform, false);
        ringObject.transform.localPosition = new Vector3(0f, 0.08f, 0f);

        _lineRenderer = ringObject.AddComponent<LineRenderer>();
        _lineRenderer.loop = true;
        _lineRenderer.useWorldSpace = false;
        _lineRenderer.positionCount = RingSegments;
        _lineRenderer.startWidth = 0.05f;
        _lineRenderer.endWidth = 0.05f;
        _lineRenderer.material = CreateLineMaterial();
        _lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _lineRenderer.receiveShadows = false;

        for (var i = 0; i < RingSegments; i++)
        {
            var angle = i / (float)RingSegments * Mathf.PI * 2f;
            var x = Mathf.Cos(angle) * 0.6f;
            var z = Mathf.Sin(angle) * 0.6f;
            _lineRenderer.SetPosition(i, new Vector3(x, 0f, z));
        }

        ApplyRoleColor();
    }

    public void UpdateRole(NpcRole role)
    {
        Role = role;
        ApplyRoleColor();
    }

    public void SetVisualizationVisible(bool isVisible)
    {
        if (_lineRenderer != null)
        {
            _lineRenderer.enabled = isVisible;
        }
    }

    private void ApplyRoleColor()
    {
        if (_lineRenderer == null)
        {
            return;
        }

        var color = Role == NpcRole.Innkeeper
            ? new Color(1f, 0.55f, 0.2f, 1f)
            : new Color(0.2f, 0.95f, 1f, 1f);

        _lineRenderer.startColor = color;
        _lineRenderer.endColor = color;
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
