using BepInEx.Logging;
using Wyrdrasil.Registry.Services;

namespace Wyrdrasil.Registry.PlayerTool;

public sealed class RegistryPlayerToolSaveService
{
    private readonly ManualLogSource _log;
    private readonly RegistryPersistenceService _persistenceService;

    public RegistryPlayerToolSaveService(
        ManualLogSource log,
        RegistryPersistenceService persistenceService)
    {
        _log = log;
        _persistenceService = persistenceService;
    }

    public void SaveAfterPersistentPlayerAction(string actionName)
    {
        _log.LogInfo($"Registry player action requested persistence save: {actionName}.");
        _persistenceService.SaveWorldState();
    }
}
