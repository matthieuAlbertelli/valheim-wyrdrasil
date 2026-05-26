using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Wyrdrasil.Settlements.Components;

/// <summary>
/// World-space glow helper for Valheim furniture whose vanilla materials are not reliably tintable.
///
/// The overlay combines a renderer-bounds ring with a lightweight duplicate mesh shell. The shell is
/// deliberately independent from the original material/shader, which makes it suitable for stubborn
/// prefabs such as workbenches where changing _Color/_EmissionColor may produce no visible result.
/// </summary>
public sealed class WyrdrasilFurnitureGlowOverlay : MonoBehaviour
{
    private sealed class MeshGlowState
    {
        public Renderer Source = null!;
        public Transform OverlayTransform = null!;
        public MeshRenderer OverlayRenderer = null!;
    }

    private const int RingSegments = 64;
    private const float BoundsPadding = 0.12f;
    private const float MeshShellScale = 1.018f;

    private readonly List<Renderer> _targetRenderers = new();
    private readonly List<MeshGlowState> _meshGlowStates = new();

    private Transform? _targetRoot;
    private LineRenderer? _baseRing;
    private LineRenderer? _midRing;
    private LineRenderer? _topRing;
    private Material? _ringMaterial;
    private Material? _meshMaterial;
    private WyrdrasilNativePieceHighlightBridge? _nativeHighlightBridge;
    private int? _nativeHighlightTargetInstanceId;
    private Color _color = Color.white;
    private bool _isVisible;
    private bool _emphasized;
    private bool _meshShellDirty = true;

    public void Initialize(Transform targetRoot)
    {
        if (_targetRoot != targetRoot)
        {
            DisableNativeHighlight();
            _targetRoot = targetRoot;
            _meshShellDirty = true;
        }

        EnsureNativeHighlightBridge();
        RefreshVisualState();
    }

    public void RegisterRenderers(IEnumerable<Renderer> renderers)
    {
        _targetRenderers.Clear();

        foreach (var renderer in renderers)
        {
            if (!IsEligibleFurnitureRenderer(renderer))
            {
                continue;
            }

            _targetRenderers.Add(renderer);
        }

        _meshShellDirty = true;
        RefreshVisualState();
    }

    public void SetVisible(bool isVisible, Color color, bool emphasized)
    {
        _isVisible = isVisible;
        _color = color;
        _emphasized = emphasized;
        RefreshVisualState();
    }

    private void LateUpdate()
    {
        if (!_isVisible)
        {
            return;
        }

        RefreshVisualState();
    }

    private void EnsureVisuals()
    {
        _ringMaterial ??= CreateRingMaterial();
        _meshMaterial ??= CreateMeshMaterial();

        _baseRing ??= CreateRing("BaseRing");
        _midRing ??= CreateRing("MidRing");
        _topRing ??= CreateRing("TopRing");
    }

    private void EnsureNativeHighlightBridge()
    {
        if (_targetRoot == null)
        {
            return;
        }

        var targetInstanceId = _targetRoot.GetInstanceID();
        if (_nativeHighlightBridge != null && _nativeHighlightTargetInstanceId == targetInstanceId)
        {
            return;
        }

        DisableNativeHighlight();
        var rootObject = _targetRoot.gameObject;
        _nativeHighlightBridge = rootObject.GetComponent<WyrdrasilNativePieceHighlightBridge>();
        if (_nativeHighlightBridge == null)
        {
            _nativeHighlightBridge = rootObject.AddComponent<WyrdrasilNativePieceHighlightBridge>();
        }

        _nativeHighlightBridge.Initialize(rootObject);
        _nativeHighlightTargetInstanceId = targetInstanceId;
    }

    private bool RefreshNativeHighlight(bool visible)
    {
        EnsureNativeHighlightBridge();
        return _nativeHighlightBridge?.SetVisible(visible, _color, _emphasized) == true;
    }

    private void DisableNativeHighlight()
    {
        _nativeHighlightBridge?.SetVisible(false, _color, emphasized: false);
        _nativeHighlightBridge = null;
        _nativeHighlightTargetInstanceId = null;
    }

    private LineRenderer CreateRing(string ringName)
    {
        var ring = new GameObject(ringName);
        ring.hideFlags = HideFlags.DontSave;
        ring.transform.SetParent(transform, false);

        var line = ring.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = true;
        line.positionCount = RingSegments;
        line.material = _ringMaterial;
        line.numCornerVertices = 4;
        line.numCapVertices = 4;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        return line;
    }

    private void RefreshVisualState()
    {
        var enabled = _isVisible && _targetRoot != null;
        if (!enabled)
        {
            SetCustomVisualsEnabled(false);
            RefreshNativeHighlight(false);
            return;
        }

        // Prefer Valheim's own build-piece highlight path. If the target is a native Piece/WearNTear,
        // Wyrdrasil must not draw a duplicate mesh shell or local material tint on top of it.
        // The ring/mesh system below is kept only as a fallback for non-piece furniture.
        if (RefreshNativeHighlight(true))
        {
            SetCustomVisualsEnabled(false);
            return;
        }

        EnsureVisuals();
        SetRingEnabled(_baseRing, true);
        SetRingEnabled(_midRing, true);
        SetRingEnabled(_topRing, true);

        if (_meshShellDirty)
        {
            RebuildMeshShells();
        }

        var bounds = ResolveTargetBounds();
        var pulse = 0.72f + (Mathf.Sin(Time.unscaledTime * 4.2f) * 0.12f);
        var alpha = _emphasized ? Mathf.Clamp01(pulse) : 0.48f;
        var ringColor = new Color(_color.r, _color.g, _color.b, alpha);
        var meshColor = new Color(_color.r, _color.g, _color.b, _emphasized ? 0.44f : 0.28f);
        var width = _emphasized ? 0.07f : 0.04f;

        ApplyMaterialColor(_ringMaterial, ringColor);
        ApplyMaterialColor(_meshMaterial, meshColor);

        ApplyRing(_baseRing, bounds, Mathf.Max(bounds.min.y + 0.04f, bounds.center.y - bounds.extents.y * 0.88f), width, ringColor);
        ApplyRing(_midRing, bounds, bounds.center.y, width * 0.74f, new Color(ringColor.r, ringColor.g, ringColor.b, ringColor.a * 0.55f));
        ApplyRing(_topRing, bounds, bounds.max.y + 0.03f, width * 0.55f, new Color(ringColor.r, ringColor.g, ringColor.b, ringColor.a * 0.42f));

        RefreshMeshShells(meshColor);
    }


    private void SetCustomVisualsEnabled(bool enabled)
    {
        SetRingEnabled(_baseRing, enabled);
        SetRingEnabled(_midRing, enabled);
        SetRingEnabled(_topRing, enabled);
        SetMeshShellsEnabled(enabled);
    }

    private void RebuildMeshShells()
    {
        ClearMeshShells();

        if (_meshMaterial == null)
        {
            _meshShellDirty = false;
            return;
        }

        foreach (var sourceRenderer in _targetRenderers)
        {
            if (sourceRenderer == null)
            {
                continue;
            }

            if (TryCreateMeshShell(sourceRenderer, out var state))
            {
                _meshGlowStates.Add(state);
            }
        }

        _meshShellDirty = false;
    }

    private bool TryCreateMeshShell(Renderer sourceRenderer, out MeshGlowState state)
    {
        state = null!;

        Mesh? mesh = null;
        if (sourceRenderer is MeshRenderer)
        {
            var meshFilter = sourceRenderer.GetComponent<MeshFilter>();
            mesh = meshFilter != null ? meshFilter.sharedMesh : null;
        }
        else if (sourceRenderer is SkinnedMeshRenderer skinnedMeshRenderer)
        {
            mesh = skinnedMeshRenderer.sharedMesh;
        }

        if (mesh == null)
        {
            return false;
        }

        var shell = new GameObject($"MeshGlow_{sourceRenderer.name}");
        shell.hideFlags = HideFlags.DontSave;
        shell.transform.SetParent(transform, false);

        var shellFilter = shell.AddComponent<MeshFilter>();
        shellFilter.sharedMesh = mesh;

        var shellRenderer = shell.AddComponent<MeshRenderer>();
        shellRenderer.sharedMaterials = CreateMaterialArrayForMesh(mesh, _meshMaterial);
        shellRenderer.shadowCastingMode = ShadowCastingMode.Off;
        shellRenderer.receiveShadows = false;
        shellRenderer.lightProbeUsage = LightProbeUsage.Off;
        shellRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

        state = new MeshGlowState
        {
            Source = sourceRenderer,
            OverlayTransform = shell.transform,
            OverlayRenderer = shellRenderer
        };
        return true;
    }


    private static Material[] CreateMaterialArrayForMesh(Mesh mesh, Material material)
    {
        var materialCount = Mathf.Max(1, mesh.subMeshCount);
        var materials = new Material[materialCount];
        for (var index = 0; index < materials.Length; index++)
        {
            materials[index] = material;
        }

        return materials;
    }

    private void RefreshMeshShells(Color color)
    {
        ApplyMaterialColor(_meshMaterial, color);

        foreach (var state in _meshGlowStates)
        {
            if (state.Source == null || state.OverlayTransform == null || state.OverlayRenderer == null)
            {
                continue;
            }

            var sourceTransform = state.Source.transform;
            state.OverlayTransform.SetPositionAndRotation(sourceTransform.position, sourceTransform.rotation);
            state.OverlayTransform.localScale = sourceTransform.lossyScale * MeshShellScale;
            state.OverlayRenderer.enabled = _isVisible && state.Source.enabled && state.Source.gameObject.activeInHierarchy;
        }
    }

    private void SetMeshShellsEnabled(bool enabled)
    {
        foreach (var state in _meshGlowStates)
        {
            if (state.OverlayRenderer != null)
            {
                state.OverlayRenderer.enabled = enabled;
            }
        }
    }

    private void ClearMeshShells()
    {
        foreach (var state in _meshGlowStates)
        {
            if (state.OverlayTransform != null)
            {
                Destroy(state.OverlayTransform.gameObject);
            }
        }

        _meshGlowStates.Clear();
    }

    private Bounds ResolveTargetBounds()
    {
        var hasBounds = false;
        var bounds = new Bounds(_targetRoot != null ? _targetRoot.position : transform.position, Vector3.zero);

        foreach (var renderer in _targetRenderers)
        {
            if (renderer == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds && _targetRoot != null)
        {
            bounds = new Bounds(_targetRoot.position + Vector3.up * 0.45f, new Vector3(1.2f, 0.9f, 1.2f));
        }

        var size = bounds.size;
        size.x = Mathf.Max(size.x + BoundsPadding * 2f, 0.7f);
        size.z = Mathf.Max(size.z + BoundsPadding * 2f, 0.7f);
        size.y = Mathf.Max(size.y, 0.35f);
        bounds.size = size;
        return bounds;
    }

    private static void ApplyRing(LineRenderer? ring, Bounds bounds, float y, float width, Color color)
    {
        if (ring == null)
        {
            return;
        }

        ring.startWidth = width;
        ring.endWidth = width;
        ring.startColor = color;
        ring.endColor = color;

        var radiusX = Mathf.Max(bounds.extents.x, 0.35f);
        var radiusZ = Mathf.Max(bounds.extents.z, 0.35f);
        var center = bounds.center;

        for (var i = 0; i < RingSegments; i++)
        {
            var angle = (i / (float)RingSegments) * Mathf.PI * 2f;
            ring.SetPosition(i, new Vector3(
                center.x + Mathf.Cos(angle) * radiusX,
                y,
                center.z + Mathf.Sin(angle) * radiusZ));
        }
    }

    private static void SetRingEnabled(LineRenderer? ring, bool enabled)
    {
        if (ring != null)
        {
            ring.enabled = enabled;
        }
    }

    private static bool IsEligibleFurnitureRenderer(Renderer? renderer)
    {
        if (renderer == null || renderer is LineRenderer)
        {
            return false;
        }

        if (renderer.GetComponentInParent<WyrdrasilFurnitureGlowOverlay>() != null)
        {
            return false;
        }

        var current = renderer.transform;
        while (current != null)
        {
            if (current.name.StartsWith("Wyrdrasil_", System.StringComparison.Ordinal))
            {
                return false;
            }

            current = current.parent;
        }

        return true;
    }

    private static Material CreateRingMaterial()
    {
        var shader = Shader.Find("Sprites/Default")
                     ?? Shader.Find("Unlit/Color")
                     ?? Shader.Find("Legacy Shaders/Transparent/Diffuse")
                     ?? Shader.Find("Standard");

        var material = new Material(shader)
        {
            name = "WyrdrasilFurnitureGlowOverlay_Ring",
            hideFlags = HideFlags.DontSave
        };

        ConfigureTransparentMaterial(material, additive: false, alwaysOnTop: false);
        return material;
    }

    private static Material CreateMeshMaterial()
    {
        var shader = Shader.Find("Hidden/Internal-Colored")
                     ?? Shader.Find("Sprites/Default")
                     ?? Shader.Find("Unlit/Color")
                     ?? Shader.Find("Legacy Shaders/Transparent/Diffuse")
                     ?? Shader.Find("Standard");

        var material = new Material(shader)
        {
            name = "WyrdrasilFurnitureGlowOverlay_MeshShell",
            hideFlags = HideFlags.DontSave
        };

        ConfigureTransparentMaterial(material, additive: true, alwaysOnTop: true);
        return material;
    }

    private static void ConfigureTransparentMaterial(Material material, bool additive, bool alwaysOnTop)
    {
        if (material.HasProperty("_SrcBlend"))
        {
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        }

        if (material.HasProperty("_DstBlend"))
        {
            material.SetInt("_DstBlend", additive ? (int)BlendMode.One : (int)BlendMode.OneMinusSrcAlpha);
        }

        if (material.HasProperty("_Cull"))
        {
            material.SetInt("_Cull", (int)CullMode.Off);
        }

        if (material.HasProperty("_ZWrite"))
        {
            material.SetInt("_ZWrite", 0);
        }

        if (alwaysOnTop && material.HasProperty("_ZTest"))
        {
            material.SetInt("_ZTest", (int)CompareFunction.Always);
        }

        material.renderQueue = alwaysOnTop
            ? (int)RenderQueue.Overlay
            : (int)RenderQueue.Transparent;
    }

    private static void ApplyMaterialColor(Material? material, Color color)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_TintColor"))
        {
            material.SetColor("_TintColor", color);
        }
    }

    private void OnDestroy()
    {
        DisableNativeHighlight();
        ClearMeshShells();

        if (_ringMaterial != null)
        {
            Destroy(_ringMaterial);
        }

        if (_meshMaterial != null)
        {
            Destroy(_meshMaterial);
        }
    }
}
