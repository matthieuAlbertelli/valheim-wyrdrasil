using UnityEngine;
using Wyrdrasil.Registry.Components;
using Wyrdrasil.Registry.Services;

namespace Wyrdrasil.Registry.PlayerTool;

public sealed class RegistryPlayerToolTargetFeedbackService
{
    private readonly RegistryResidentService _residentService;

    private WyrdrasilRegistryTargetCharacterHoverMarker? _currentMarker;

    public RegistryPlayerToolTargetFeedbackService(RegistryResidentService residentService)
    {
        _residentService = residentService;
    }

    public void Update()
    {
        var player = Player.m_localPlayer;
        if (!RegistryPlayerToolSelectionService.IsRegistryToolActive(player))
        {
            ClearCurrentMarker();
            return;
        }

        if (!RegistryPlayerToolWorldTargeting.TryGetTargetCharacter(out var character))
        {
            ClearCurrentMarker();
            return;
        }

        var marker = character.GetComponent<WyrdrasilRegistryTargetCharacterHoverMarker>() ??
                     character.gameObject.AddComponent<WyrdrasilRegistryTargetCharacterHoverMarker>();

        if (_currentMarker != null && _currentMarker != marker)
        {
            _currentMarker.Hide();
        }

        var isRegistered = _residentService.TryGetRegisteredResidentForCharacter(character, out _);
        marker.SetState(true, isRegistered);
        _currentMarker = marker;
    }

    private void ClearCurrentMarker()
    {
        if (_currentMarker == null)
        {
            return;
        }

        _currentMarker.Hide();
        _currentMarker = null;
    }
}
