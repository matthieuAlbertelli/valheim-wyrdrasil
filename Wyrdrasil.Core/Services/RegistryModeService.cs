using System;
using BepInEx.Logging;
using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Core.Services;


public sealed class RegistryModeService
{
    private readonly ManualLogSource _log;

    public RegistryToolState State { get; } = new();

    public bool IsRegistryModeEnabled => State.IsRegistryModeEnabled;

    public event Action<bool>? RegistryModeChanged;

    public RegistryModeService(ManualLogSource log)
    {
        _log = log;
    }

    public void EnableRegistryMode()
    {
        if (State.IsRegistryModeEnabled)
        {
            return;
        }

        State.EnableRegistryMode();
        _log.LogInfo("Registry mode enabled.");
        RegistryModeChanged?.Invoke(true);
    }

    public void DisableRegistryMode()
    {
        if (!State.IsRegistryModeEnabled)
        {
            return;
        }

        State.DisableRegistryMode();
        _log.LogInfo("Registry mode disabled.");
        RegistryModeChanged?.Invoke(false);
    }

    public void ToggleRegistryMode()
    {
        if (State.IsRegistryModeEnabled)
        {
            DisableRegistryMode();
            return;
        }

        EnableRegistryMode();
    }
}
