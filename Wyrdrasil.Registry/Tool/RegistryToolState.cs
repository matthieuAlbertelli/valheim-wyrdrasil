namespace Wyrdrasil.Registry.Tool;

public sealed class RegistryToolState
{
    public bool IsRegistryModeEnabled { get; private set; }

    public RegistryCategory SelectedCategory { get; private set; } = RegistryCategory.Zones;

    public RegistryActionType SelectedAction { get; private set; } = RegistryActionType.CreateTavernZone;

    public void EnableRegistryMode()
    {
        IsRegistryModeEnabled = true;
    }

    public void DisableRegistryMode()
    {
        IsRegistryModeEnabled = false;
    }

    public void ToggleRegistryMode()
    {
        IsRegistryModeEnabled = !IsRegistryModeEnabled;
    }

    public void SetSelectedCategory(RegistryCategory category)
    {
        SelectedCategory = category;
    }

    public void SetSelectedAction(RegistryActionType action)
    {
        SelectedAction = action;
    }
}
