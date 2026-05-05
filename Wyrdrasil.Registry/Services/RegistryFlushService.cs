using BepInEx.Logging;
using Wyrdrasil.Settlements.Runtime;

namespace Wyrdrasil.Registry.Services;

public sealed class RegistryFlushService
{
    private readonly ManualLogSource _log;
    private readonly ISettlementsRuntimeApi _settlementsRuntimeApi;
    private readonly RegistryResidentService _residentService;
    private readonly RegistryPersistenceService _persistenceService;

    public RegistryFlushService(
        ManualLogSource log,
        ISettlementsRuntimeApi settlementsRuntimeApi,
        RegistryResidentService residentService,
        RegistryPersistenceService persistenceService)
    {
        _log = log;
        _settlementsRuntimeApi = settlementsRuntimeApi;
        _residentService = residentService;
        _persistenceService = persistenceService;
    }

    public void FlushAllRegistryState()
    {
        _residentService.ClearAllResidents();
        _settlementsRuntimeApi.ClearAllState();
        _persistenceService.DeleteCurrentWorldSave();
        _log.LogInfo("Flushed all in-memory registry state. The next save will write an empty registry state for the current world.");
    }
}
