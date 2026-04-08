using System.Collections.Generic;
using UnityEngine;

namespace Wyrdrasil.Settlements.Components;


public sealed class WyrdrasilRegisteredBedMarker : MonoBehaviour
{
    private sealed class MaterialState
    {
        public Renderer Renderer = null!;
        public Material[] Materials = null!;
        public Color[] OriginalBaseColors = null!;
        public Color[] OriginalEmissionColors = null!;
    }

    private readonly List<MaterialState> _states = new();

    private bool _isVisible;
    private bool _isAssigned;
    private bool _isPendingForceAssignTarget;
    private int _bedId;

    public void Initialize(int bedId)
    {
        _bedId = bedId;
    }

    public void RegisterRenderers(Renderer[] renderers)
    {
        _states.Clear();

        if (renderers == null)
        {
            return;
        }

        foreach (var renderer in renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            var materials = renderer.materials;
            var baseColors = new Color[materials.Length];
            var emissionColors = new Color[materials.Length];

            for (var i = 0; i < materials.Length; i++)
            {
                var material = materials[i];
                baseColors[i] = material.HasProperty("_Color") ? material.color : Color.white;
                emissionColors[i] = material.HasProperty("_EmissionColor") ? material.GetColor("_EmissionColor") : Color.black;
            }

            _states.Add(new MaterialState
            {
                Renderer = renderer,
                Materials = materials,
                OriginalBaseColors = baseColors,
                OriginalEmissionColors = emissionColors
            });
        }

        ApplyVisualization();
    }

    public void SetVisualizationVisible(bool isVisible, bool isAssigned)
    {
        _isVisible = isVisible;
        _isAssigned = isAssigned;
        ApplyVisualization();
    }

    public void SetPendingForceAssignTarget(bool isPending)
    {
        _isPendingForceAssignTarget = isPending;
        ApplyVisualization();
    }

    private void ApplyVisualization()
    {
        foreach (var state in _states)
        {
            for (var i = 0; i < state.Materials.Length; i++)
            {
                var material = state.Materials[i];
                if (material == null)
                {
                    continue;
                }

                if (!_isVisible)
                {
                    RestoreMaterial(material, state, i);
                    continue;
                }

                var glowColor = _isPendingForceAssignTarget
                    ? new Color(1f, 0.2f, 0.2f, 1f)
                    : _isAssigned
                        ? new Color(0.25f, 1f, 0.35f, 1f)
                        : new Color(0.75f, 0.45f, 1f, 1f);

                if (material.HasProperty("_Color"))
                {
                    material.color = Color.Lerp(state.OriginalBaseColors[i], glowColor, 0.18f);
                }

                if (material.HasProperty("_EmissionColor"))
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", glowColor * 1.35f);
                    DynamicGI.SetEmissive(state.Renderer, glowColor * 1.35f);
                }
            }
        }
    }

    private static void RestoreMaterial(Material material, MaterialState state, int index)
    {
        if (material.HasProperty("_Color"))
        {
            material.color = state.OriginalBaseColors[index];
        }

        if (material.HasProperty("_EmissionColor"))
        {
            material.SetColor("_EmissionColor", state.OriginalEmissionColors[index]);
            if (state.OriginalEmissionColors[index].maxColorComponent <= 0.0001f)
            {
                material.DisableKeyword("_EMISSION");
            }

            DynamicGI.SetEmissive(state.Renderer, state.OriginalEmissionColors[index]);
        }
    }
}
