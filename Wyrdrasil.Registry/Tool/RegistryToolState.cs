namespace Wyrdrasil.Registry.Tool;

public sealed class RegistryToolState
{
    public bool IsRegistryModeEnabled { get; private set; }

    public RegistryCategory SelectedCategory { get; private set; } = RegistryCategory.Zones;

    public RegistryActionType SelectedAction { get; private set; } = RegistryActionType.CreateTavernZone;

    public int? PendingResidentForceAssignId { get; private set; }

    public string PendingResidentForceAssignName { get; private set; } = string.Empty;

    public void EnableRegistryMode()
    {
        IsRegistryModeEnabled = true;
    }

    public void DisableRegistryMode()
    {
        IsRegistryModeEnabled = false;
        ClearPendingResidentForceAssign();
    }

    public void ToggleRegistryMode()
    {
        IsRegistryModeEnabled = !IsRegistryModeEnabled;

        if (!IsRegistryModeEnabled)
        {
            ClearPendingResidentForceAssign();
        }
    }

    public void SetSelectedCategory(RegistryCategory category)
    {
        SelectedCategory = category;
    }

    public void SetSelectedAction(RegistryActionType action)
    {
        SelectedAction = action;
    }

    public void SetPendingResidentForceAssign(int residentId, string displayName)
    {
        PendingResidentForceAssignId = residentId;
        PendingResidentForceAssignName = displayName ?? string.Empty;
    }

    public void ClearPendingResidentForceAssign()
    {
        PendingResidentForceAssignId = null;
        PendingResidentForceAssignName = string.Empty;
    }
}
