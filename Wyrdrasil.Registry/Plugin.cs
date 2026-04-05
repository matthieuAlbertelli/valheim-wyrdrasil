using BepInEx;
using BepInEx.Logging;
using UnityEngine;

namespace Wyrdrasil.Registry;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.wyrdrasil.registry";
    public const string PluginName = "Wyrdrasil.Registry";
    public const string PluginVersion = "0.1.0";

    internal static ManualLogSource Log = null!;

    private bool _isRegistryModeEnabled;

    private void Awake()
    {
        Log = Logger;
        Log.LogInfo($"{PluginName} {PluginVersion} loaded.");
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F8))
        {
            ToggleRegistryMode();
        }
    }

    private void ToggleRegistryMode()
    {
        _isRegistryModeEnabled = !_isRegistryModeEnabled;

        if (_isRegistryModeEnabled)
        {
            Log.LogInfo("Registry mode enabled.");
        }
        else
        {
            Log.LogInfo("Registry mode disabled.");
        }
    }
}