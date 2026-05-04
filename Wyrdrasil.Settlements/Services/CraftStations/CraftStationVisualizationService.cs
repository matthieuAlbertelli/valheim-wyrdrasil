using System;
using System.Collections.Generic;
using UnityEngine;
using Wyrdrasil.Core.Services;
using Wyrdrasil.Settlements.Components;
using Wyrdrasil.Settlements.Tool;

namespace Wyrdrasil.Settlements.Services.CraftStations;

public sealed class CraftStationVisualizationService
{
    private readonly Dictionary<int, WyrdrasilRegisteredCraftStationMarker> _markers = new();
    private readonly Dictionary<int, GameObject> _anchorRoots = new();
    private readonly Func<IReadOnlyList<RegisteredCraftStationData>> _craftStationProvider;
    private bool _visualsVisible;
    private int? _pendingConstructionTargetCraftStationId;

    public CraftStationVisualizationService(RegistryModeService modeService, Func<IReadOnlyList<RegisteredCraftStationData>> craftStationProvider)
    {
        _craftStationProvider = craftStationProvider;
        _visualsVisible = modeService.IsRegistryModeEnabled;
        modeService.RegistryModeChanged += OnRegistryModeChanged;
    }

    public void RebuildAllVisuals()
    {
        ClearAllVisuals();
        foreach (var station in _craftStationProvider())
        {
            RegisterStation(station);
            RefreshStation(station);
        }
    }

    public void ClearAllVisuals()
    {
        foreach (var station in _craftStationProvider())
        {
            if (_markers.TryGetValue(station.Id, out var marker) && marker != null)
            {
                marker.SetVisualizationVisible(false, false);
            }
        }

        foreach (var root in _anchorRoots.Values)
        {
            if (root != null)
            {
                UnityEngine.Object.Destroy(root);
            }
        }

        _markers.Clear();
        _anchorRoots.Clear();
    }

    public void RegisterStation(RegisteredCraftStationData station)
    {
        EnsureMarker(station);
        EnsureAnchorIndicator(station);
    }

    public void RefreshStation(RegisteredCraftStationData station)
    {
        UpdateMarker(station);
        UpdateAnchorIndicator(station);
    }

    public void UnregisterStation(RegisteredCraftStationData station)
    {
        if (_markers.TryGetValue(station.Id, out var marker) && marker != null)
        {
            marker.SetVisualizationVisible(false, false);
        }

        if (_anchorRoots.TryGetValue(station.Id, out var anchorRoot) && anchorRoot != null)
        {
            UnityEngine.Object.Destroy(anchorRoot);
        }

        _markers.Remove(station.Id);
        _anchorRoots.Remove(station.Id);
    }

    public void SetPendingConstructionTarget(int? craftStationId)
    {
        _pendingConstructionTargetCraftStationId = craftStationId;
        foreach (var station in _craftStationProvider())
        {
            UpdateMarker(station);
        }
    }

    private void OnRegistryModeChanged(bool isEnabled)
    {
        _visualsVisible = isEnabled;
        foreach (var station in _craftStationProvider())
        {
            RefreshStation(station);
        }
    }

    private void EnsureMarker(RegisteredCraftStationData station)
    {
        if (station.FurnitureRoot == null || _markers.ContainsKey(station.Id))
        {
            return;
        }

        var marker = station.FurnitureRoot.GetComponent<WyrdrasilRegisteredCraftStationMarker>();
        if (marker == null)
        {
            marker = station.FurnitureRoot.AddComponent<WyrdrasilRegisteredCraftStationMarker>();
        }

        marker.Initialize(station.Id);
        marker.RegisterRenderers(station.FurnitureRoot.GetComponentsInChildren<Renderer>(true));
        _markers[station.Id] = marker;
    }

    private void UpdateMarker(RegisteredCraftStationData station)
    {
        if (_markers.TryGetValue(station.Id, out var marker) && marker != null)
        {
            marker.SetVisualizationVisible(_visualsVisible, station.AssignedRegisteredNpcId.HasValue);
            marker.SetPendingConstructionTarget(_pendingConstructionTargetCraftStationId == station.Id);
        }
    }

    private void EnsureAnchorIndicator(RegisteredCraftStationData station)
    {
        if (_anchorRoots.ContainsKey(station.Id))
        {
            return;
        }

        var root = new GameObject($"Wyrdrasil_CraftStationAnchor_{station.Id}");
        _anchorRoots[station.Id] = root;

        var anchor = CreateIndicatorPrimitive(root.transform, PrimitiveType.Sphere, "Anchor", new Vector3(0.18f, 0.18f, 0.18f), new Color(0.25f, 1f, 0.35f, 1f));
        var shaft = CreateIndicatorPrimitive(root.transform, PrimitiveType.Cube, "ArrowShaft", new Vector3(0.08f, 0.08f, 0.65f), new Color(1f, 0.25f, 0.25f, 1f));
        var head = CreateIndicatorPrimitive(root.transform, PrimitiveType.Cube, "ArrowHead", new Vector3(0.18f, 0.18f, 0.18f), new Color(1f, 0.25f, 0.25f, 1f));

        anchor.transform.localPosition = Vector3.zero;
        shaft.transform.localPosition = new Vector3(0f, 0f, 0.38f);
        head.transform.localPosition = new Vector3(0f, 0f, 0.72f);
    }

    private void UpdateAnchorIndicator(RegisteredCraftStationData station)
    {
        if (!_anchorRoots.TryGetValue(station.Id, out var root) || root == null)
        {
            return;
        }

        root.SetActive(_visualsVisible);
        if (!_visualsVisible)
        {
            return;
        }

        if (!station.TryResolveWorldAnchor(out var anchorWorldPosition, out var anchorWorldForward))
        {
            root.SetActive(false);
            return;
        }

        root.transform.position = anchorWorldPosition + Vector3.up * 0.05f;
        root.transform.rotation = Quaternion.LookRotation(anchorWorldForward, Vector3.up);
    }

    private static GameObject CreateIndicatorPrimitive(Transform parent, PrimitiveType primitiveType, string name, Vector3 scale, Color color)
    {
        var gameObject = GameObject.CreatePrimitive(primitiveType);
        gameObject.name = name;
        gameObject.transform.SetParent(parent, false);
        gameObject.transform.localScale = scale;

        var collider = gameObject.GetComponent<Collider>();
        if (collider != null)
        {
            UnityEngine.Object.Destroy(collider);
        }

        var renderer = gameObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            var shader = Shader.Find("Sprites/Default");
            if (!shader)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader)
            {
                var material = new Material(shader);
                material.color = color;
                renderer.material = material;
            }
        }

        return gameObject;
    }
}
