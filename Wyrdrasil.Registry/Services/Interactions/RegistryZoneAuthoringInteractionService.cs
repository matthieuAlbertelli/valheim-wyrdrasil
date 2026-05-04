using UnityEngine;
using Wyrdrasil.Core.Tool;
using Wyrdrasil.Settlements.Authoring;
using Wyrdrasil.Settlements.Tool;

namespace Wyrdrasil.Registry.Services.Interactions;

public sealed class RegistryZoneAuthoringInteractionService
{
    private readonly ISettlementsAuthoringApi _settlementsAuthoringApi;

    public RegistryZoneAuthoringInteractionService(ISettlementsAuthoringApi settlementsAuthoringApi)
    {
        _settlementsAuthoringApi = settlementsAuthoringApi;
    }

    public PendingZoneAuthoringSnapshot? CurrentSnapshot => _settlementsAuthoringApi.GetPendingZoneAuthoringSnapshot();

    public bool IsActive => _settlementsAuthoringApi.IsZoneAuthoringActive;

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
            _settlementsAuthoringApi.UpdatePendingZoneAuthoringPreview();
        }
    }

    public bool TryHandleSecondaryInput()
    {
        if (!Input.GetMouseButtonDown(1))
        {
            return false;
        }

        _settlementsAuthoringApi.HandleZoneAuthoringSecondaryInput();
        return true;
    }

    public bool TryHandleHeightAdjustment()
    {
        if (!_settlementsAuthoringApi.IsZoneHeightEditingActive)
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
        _settlementsAuthoringApi.AdjustPendingZoneHeight(direction, adjustBase);
        return true;
    }

    public void CancelIfActive()
    {
        if (_settlementsAuthoringApi.IsZoneAuthoringActive)
        {
            _settlementsAuthoringApi.CancelPendingZoneAuthoring();
        }
    }
}
