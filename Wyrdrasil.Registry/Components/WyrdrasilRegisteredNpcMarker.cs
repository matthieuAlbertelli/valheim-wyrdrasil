using System.Collections.Generic;
using UnityEngine;
using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Components;

public sealed class WyrdrasilRegisteredNpcMarker : MonoBehaviour
{
    private const int RingSegments = 24;
    private const int PendingDashCount = 12;
    private const float PendingDashDegrees = 14f;
    private const float PendingRotationSpeed = 120f;

    private LineRenderer? _lineRenderer;
    private Transform? _pendingDashRoot;
    private readonly List<LineRenderer> _pendingDashRenderers = new();
    private bool _isVisible;
    private bool _isPendingForceAssign;

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

        CreateNormalRing();
        CreatePendingRing();
        ApplyRoleColor();
        ApplyPendingColor();
        RefreshVisualization();
    }

    public void UpdateRole(NpcRole role)
    {
        Role = role;
        ApplyRoleColor();
    }

    public void SetVisualizationVisible(bool isVisible)
    {
        _isVisible = isVisible;
        RefreshVisualization();
    }

    public void SetPendingForceAssign(bool isPending)
    {
        _isPendingForceAssign = isPending;
        RefreshVisualization();
    }

    private void Update()
    {
        if (_isVisible && _isPendingForceAssign && _pendingDashRoot != null)
        {
            _pendingDashRoot.Rotate(0f, PendingRotationSpeed * Time.deltaTime, 0f, Space.Self);
        }
    }

    private void CreateNormalRing()
    {
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
    }

    private void CreatePendingRing()
    {
        var pendingRootObject = new GameObject("WyrdrasilResidentPendingRing");
        pendingRootObject.transform.SetParent(transform, false);
        pendingRootObject.transform.localPosition = new Vector3(0f, 0.08f, 0f);
        _pendingDashRoot = pendingRootObject.transform;

        for (var i = 0; i < PendingDashCount; i++)
        {
            var dashObject = new GameObject($"Dash_{i}");
            dashObject.transform.SetParent(_pendingDashRoot, false);

            var lineRenderer = dashObject.AddComponent<LineRenderer>();
            lineRenderer.loop = false;
            lineRenderer.useWorldSpace = false;
            lineRenderer.positionCount = 2;
            lineRenderer.startWidth = 0.065f;
            lineRenderer.endWidth = 0.065f;
            lineRenderer.material = CreateLineMaterial();
            lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;

            var angleStart = i / (float)PendingDashCount * Mathf.PI * 2f;
            var angleEnd = angleStart + Mathf.Deg2Rad * PendingDashDegrees;
            var radius = 0.72f;

            lineRenderer.SetPosition(0, new Vector3(Mathf.Cos(angleStart) * radius, 0f, Mathf.Sin(angleStart) * radius));
            lineRenderer.SetPosition(1, new Vector3(Mathf.Cos(angleEnd) * radius, 0f, Mathf.Sin(angleEnd) * radius));
            _pendingDashRenderers.Add(lineRenderer);
        }
    }

    private void RefreshVisualization()
    {
        var showNormalRing = _isVisible && !_isPendingForceAssign;
        var showPendingRing = _isVisible && _isPendingForceAssign;

        if (_lineRenderer != null)
        {
            _lineRenderer.enabled = showNormalRing;
        }

        if (_pendingDashRoot != null)
        {
            _pendingDashRoot.gameObject.SetActive(showPendingRing);
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

    private void ApplyPendingColor()
    {
        var color = new Color(1f, 0.95f, 0.35f, 1f);

        foreach (var dashRenderer in _pendingDashRenderers)
        {
            if (dashRenderer == null)
            {
                continue;
            }

            dashRenderer.startColor = color;
            dashRenderer.endColor = color;
        }
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
