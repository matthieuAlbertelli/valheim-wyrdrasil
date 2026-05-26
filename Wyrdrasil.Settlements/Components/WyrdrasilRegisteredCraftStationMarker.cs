using System;
using System.Collections.Generic;
using UnityEngine;
using Wyrdrasil.Core.Tool;

namespace Wyrdrasil.Settlements.Components;

public sealed class WyrdrasilRegisteredCraftStationMarker : MonoBehaviour
{
    private sealed class MaterialSnapshot
    {
        public bool HasColor;
        public bool HasBaseColor;
        public bool HasTintColor;
        public bool HasEmissionColor;
        public Color Color;
        public Color BaseColor;
        public Color TintColor;
        public Color EmissionColor;
    }

    private sealed class MaterialState
    {
        public Renderer Renderer = null!;
        public Material[] Materials = null!;
        public MaterialSnapshot[] Snapshots = null!;
    }

    private readonly List<MaterialState> _states = new();
    private bool _isVisible;
    private bool _isAssigned;
    private bool _isPendingConstructionTarget;
    private int _craftStationId;
    private WyrdrasilFurnitureGlowOverlay? _glowOverlay;

    public int CraftStationId => _craftStationId;

    public void Initialize(int craftStationId)
    {
        _craftStationId = craftStationId;
    }

    public void RegisterRenderers(Renderer[] renderers)
    {
        _states.Clear();

        var visualRenderers = new List<Renderer>();
        if (renderers != null)
        {
            foreach (var renderer in renderers)
            {
                if (!IsEligibleFurnitureRenderer(renderer))
                {
                    continue;
                }

                visualRenderers.Add(renderer);

                var materials = renderer.materials;
                var snapshots = new MaterialSnapshot[materials.Length];
                for (var i = 0; i < materials.Length; i++)
                {
                    snapshots[i] = CaptureSnapshot(materials[i]);
                }

                _states.Add(new MaterialState
                {
                    Renderer = renderer,
                    Materials = materials,
                    Snapshots = snapshots
                });
            }
        }

        EnsureGlowOverlay().RegisterRenderers(visualRenderers);
        ApplyVisualization();
    }

    public void SetVisualizationVisible(bool isVisible, bool isAssigned)
    {
        _isVisible = isVisible;
        _isAssigned = isAssigned;
        ApplyVisualization();
    }

    public void SetPendingConstructionTarget(bool isPending)
    {
        _isPendingConstructionTarget = isPending;
        ApplyVisualization();
    }

    private void ApplyVisualization()
    {
        var glowColor = ResolveGlowColor();
        var tintStrength = ResolveTintStrength();
        var emissionStrength = ResolveEmissionStrength();
        var overlayShouldBeVisible = _isVisible && (_isAssigned || _isPendingConstructionTarget);

        // Build pieces such as workbenches should be owned by Valheim's native highlight path.
        // If we also mutate renderer materials here, the result looks like Wyrdrasil's custom
        // overlay instead of the same native piece highlight used by the hammer.
        var nativePieceHighlightShouldOwnVisualization = overlayShouldBeVisible && CanUseNativePieceHighlight();

        foreach (var state in _states)
        {
            for (var i = 0; i < state.Materials.Length; i++)
            {
                var material = state.Materials[i];
                if (material == null)
                {
                    continue;
                }

                if (!_isVisible || nativePieceHighlightShouldOwnVisualization)
                {
                    RestoreMaterial(material, state.Snapshots[i], state.Renderer);
                    continue;
                }

                ApplyTint(material, state.Snapshots[i], glowColor, tintStrength);
                ApplyEmission(material, state.Snapshots[i], state.Renderer, glowColor, emissionStrength);
            }
        }

        EnsureGlowOverlay().SetVisible(overlayShouldBeVisible, glowColor, _isAssigned || _isPendingConstructionTarget);
    }

    private Color ResolveGlowColor()
    {
        if (_isPendingConstructionTarget)
        {
            return new Color(1f, 0.95f, 0.35f, 1f);
        }

        return _isAssigned
            ? WyrdrasilVisualizationPalette.AssignedPurple
            : new Color(1f, 0.75f, 0.2f, 1f);
    }

    private float ResolveTintStrength()
    {
        if (_isPendingConstructionTarget)
        {
            return 0.45f;
        }

        return _isAssigned ? 0.72f : 0.22f;
    }

    private float ResolveEmissionStrength()
    {
        if (_isPendingConstructionTarget)
        {
            return 1.85f;
        }

        return _isAssigned ? 2.35f : 1.35f;
    }


    private bool CanUseNativePieceHighlight()
    {
        return GetComponent<WearNTear>() != null ||
               GetComponentInParent<WearNTear>() != null ||
               GetComponentInChildren<WearNTear>(true) != null ||
               GetComponent<Piece>() != null ||
               GetComponentInParent<Piece>() != null ||
               GetComponentInChildren<Piece>(true) != null;
    }

    private WyrdrasilFurnitureGlowOverlay EnsureGlowOverlay()
    {
        if (_glowOverlay != null)
        {
            return _glowOverlay;
        }

        var overlayRoot = transform.Find("Wyrdrasil_CraftStationGlowOverlay");
        if (overlayRoot == null)
        {
            var overlayObject = new GameObject("Wyrdrasil_CraftStationGlowOverlay");
            overlayObject.hideFlags = HideFlags.DontSave;
            overlayObject.transform.SetParent(transform, false);
            overlayRoot = overlayObject.transform;
        }

        _glowOverlay = overlayRoot.GetComponent<WyrdrasilFurnitureGlowOverlay>();
        if (_glowOverlay == null)
        {
            _glowOverlay = overlayRoot.gameObject.AddComponent<WyrdrasilFurnitureGlowOverlay>();
        }

        _glowOverlay.Initialize(transform);
        return _glowOverlay;
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
            if (current.name.StartsWith("Wyrdrasil_", StringComparison.Ordinal))
            {
                return false;
            }

            current = current.parent;
        }

        return true;
    }

    private static MaterialSnapshot CaptureSnapshot(Material material)
    {
        var snapshot = new MaterialSnapshot();
        if (material == null)
        {
            return snapshot;
        }

        snapshot.HasColor = material.HasProperty("_Color");
        snapshot.HasBaseColor = material.HasProperty("_BaseColor");
        snapshot.HasTintColor = material.HasProperty("_TintColor");
        snapshot.HasEmissionColor = material.HasProperty("_EmissionColor");

        snapshot.Color = snapshot.HasColor ? material.GetColor("_Color") : Color.white;
        snapshot.BaseColor = snapshot.HasBaseColor ? material.GetColor("_BaseColor") : Color.white;
        snapshot.TintColor = snapshot.HasTintColor ? material.GetColor("_TintColor") : Color.white;
        snapshot.EmissionColor = snapshot.HasEmissionColor ? material.GetColor("_EmissionColor") : Color.black;
        return snapshot;
    }

    private static void ApplyTint(Material material, MaterialSnapshot snapshot, Color glowColor, float tintStrength)
    {
        if (snapshot.HasColor)
        {
            material.SetColor("_Color", Color.Lerp(snapshot.Color, glowColor, tintStrength));
        }

        if (snapshot.HasBaseColor)
        {
            material.SetColor("_BaseColor", Color.Lerp(snapshot.BaseColor, glowColor, tintStrength));
        }

        if (snapshot.HasTintColor)
        {
            material.SetColor("_TintColor", Color.Lerp(snapshot.TintColor, glowColor, tintStrength));
        }
    }

    private static void ApplyEmission(
        Material material,
        MaterialSnapshot snapshot,
        Renderer renderer,
        Color glowColor,
        float emissionStrength)
    {
        if (!snapshot.HasEmissionColor)
        {
            return;
        }

        var emissionColor = glowColor * emissionStrength;
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", emissionColor);
        DynamicGI.SetEmissive(renderer, emissionColor);
    }

    private static void RestoreMaterial(Material material, MaterialSnapshot snapshot, Renderer renderer)
    {
        if (snapshot.HasColor)
        {
            material.SetColor("_Color", snapshot.Color);
        }

        if (snapshot.HasBaseColor)
        {
            material.SetColor("_BaseColor", snapshot.BaseColor);
        }

        if (snapshot.HasTintColor)
        {
            material.SetColor("_TintColor", snapshot.TintColor);
        }

        if (snapshot.HasEmissionColor)
        {
            material.SetColor("_EmissionColor", snapshot.EmissionColor);
            if (snapshot.EmissionColor.maxColorComponent <= 0.0001f)
            {
                material.DisableKeyword("_EMISSION");
            }

            DynamicGI.SetEmissive(renderer, snapshot.EmissionColor);
        }
    }
}
