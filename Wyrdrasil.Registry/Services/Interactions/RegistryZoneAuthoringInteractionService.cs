using UnityEngine;
using Wyrdrasil.Core.Tool;
using Wyrdrasil.Settlements.Services;
using Wyrdrasil.Settlements.Tool;

namespace Wyrdrasil.Registry.Services.Interactions;

public sealed class RegistryZoneAuthoringInteractionService
{
    private readonly FunctionalZoneService _zoneService;

    public RegistryZoneAuthoringInteractionService(FunctionalZoneService zoneService)
    {
        _zoneService = zoneService;
    }

    public PendingZoneAuthoringSnapshot? CurrentSnapshot => _zoneService.GetPendingZoneAuthoringSnapshot();

    public bool IsActive => _zoneService.IsZoneAuthoringActive;

    public bool ShouldHandle(RegistryActionType selectedAction)
    {
        return IsActive ||
               selectedAction == RegistryActionType.CreateTavernZone ||
               selectedAction == RegistryActionType.CreateBedroomZone;
    }

    public void UpdatePreviewIfNeeded(RegistryActionType selectedAction)
    {
        if (selectedAction == RegistryActionType.CreateTavernZone || selectedAction == RegistryActionType.CreateBedroomZone)
        {
            _zoneService.UpdatePendingZoneAuthoringPreview();
        }
    }

    public bool TryHandleSecondaryInput()
    {
        if (!Input.GetMouseButtonDown(1))
        {
            return false;
        }

        _zoneService.HandleZoneAuthoringSecondaryInput();
        return true;
    }

    public bool TryHandleHeightAdjustment()
    {
        if (!_zoneService.IsZoneHeightEditingActive)
        {
            return false;
        }

        var scrollDelta = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scrollDelta) <= 0.01f)
        {
            return false;
        }

        var direction = scrollDelta > 0f ? 1 : -1;
        var adjustBase = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        _zoneService.AdjustPendingZoneHeight(direction, adjustBase);
        return true;
    }

    public void CancelIfActive()
    {
        if (_zoneService.IsZoneAuthoringActive)
        {
            _zoneService.CancelPendingZoneAuthoring();
        }
    }
}
