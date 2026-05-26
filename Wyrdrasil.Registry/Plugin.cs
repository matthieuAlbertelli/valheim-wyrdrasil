using BepInEx;
using HarmonyLib;
using Wyrdrasil.Registry.PlayerTool.Visual;

namespace Wyrdrasil.Registry;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.wyrdrasil.registry";
    public const string PluginName = "Wyrdrasil.Registry";
    public const string PluginVersion = "0.1.0";

    private RegistryModuleRuntime _moduleRuntime = null!;
    private Harmony? _harmony;

    private void Awake()
    {
        _harmony = new Harmony(PluginGuid);
        var playerToolVisualConfig = RegistryPlayerToolVisualConfig.Bind(Config);
        _moduleRuntime = RegistryModuleBootstrap.Create(
            Logger,
            _harmony,
            Info.Location,
            playerToolVisualConfig);
        Logger.LogInfo($"{PluginName} {PluginVersion} loaded.");
    }

    private void Update()
    {
        _moduleRuntime.Update();
    }

    private void OnGUI()
    {
        _moduleRuntime.OnGUI();
    }

    private void OnDestroy()
    {
        _moduleRuntime?.Shutdown();
        _harmony?.UnpatchSelf();
    }
}
