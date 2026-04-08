using System.Collections.Generic;
using Wyrdrasil.Registry.Components;
using Wyrdrasil.Registry.Tool;
using Wyrdrasil.Core.Services;
using Wyrdrasil.Souls.Services;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Registry.Services;

public sealed class RegistryResidentVisualService
{
    private readonly RegistryResidentRuntimeService _runtimeService;
    private readonly Dictionary<int, WyrdrasilRegisteredNpcMarker> _markers = new();

    private bool _visualsVisible;

    public IReadOnlyDictionary<int, WyrdrasilRegisteredNpcMarker> Markers => _markers;

    public RegistryResidentVisualService(RegistryModeService modeService, RegistryResidentRuntimeService runtimeService)
    {
        _runtimeService = runtimeService;
        _visualsVisible = modeService.IsRegistryModeEnabled;
        modeService.RegistryModeChanged += OnRegistryModeChanged;
    }

    public void EnsureMarker(RegisteredNpcData data)
    {
        if (!_runtimeService.TryGetBoundCharacter(data.Id, out var character))
        {
            return;
        }

        var marker = character.GetComponent<WyrdrasilRegisteredNpcMarker>() ?? character.gameObject.AddComponent<WyrdrasilRegisteredNpcMarker>();
        marker.Initialize(data.Id, data.DisplayName, data.Role);
        marker.EnsureVisual();
        marker.SetVisualizationVisible(_visualsVisible);
        _markers[data.Id] = marker;
    }

    public void UpdateMarker(RegisteredNpcData data)
    {
        if (_markers.TryGetValue(data.Id, out var marker))
        {
            marker.UpdateRole(data.Role);
        }
    }

    public void SetPendingForceAssignResidentVisual(int? residentId)
    {
        foreach (var pair in _markers)
        {
            if (pair.Value == null)
            {
                continue;
            }

            pair.Value.SetPendingForceAssign(residentId.HasValue && pair.Key == residentId.Value);
        }
    }

    public void ClearAll()
    {
        foreach (var marker in _markers.Values)
        {
            if (marker == null)
            {
                continue;
            }

            marker.SetPendingForceAssign(false);
            marker.SetVisualizationVisible(false);
        }

        _markers.Clear();
    }

    private void OnRegistryModeChanged(bool isEnabled)
    {
        _visualsVisible = isEnabled;
        foreach (var marker in _markers.Values)
        {
            if (marker == null)
            {
                continue;
            }

            marker.SetVisualizationVisible(isEnabled);
        }
    }
}
