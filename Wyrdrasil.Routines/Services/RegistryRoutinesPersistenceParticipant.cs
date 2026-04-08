using Wyrdrasil.Core.Persistence;
using Wyrdrasil.Registry.Tool;
using Wyrdrasil.Routines.Tool;

namespace Wyrdrasil.Routines.Services;


public sealed class RegistryRoutinesPersistenceParticipant : IWorldPersistenceParticipant
{
    private readonly RegistryWorldClockService _worldClockService;

    public RegistryRoutinesPersistenceParticipant(RegistryWorldClockService worldClockService)
    {
        _worldClockService = worldClockService;
    }

    public string ModuleId => "routines";
    public int SchemaVersion => 1;

    public void ResetForWorldChange()
    {
    }

    public string CapturePayload()
    {
        var saveData = new RoutinesModuleSaveData();
        if (_worldClockService.TryGetSimulatedMinuteOfDay(out var minuteOfDay))
        {
            saveData.HasSimulatedMinuteOfDay = true;
            saveData.SimulatedMinuteOfDay = minuteOfDay;
        }

        return WorldPersistenceCoordinator.SerializePayload(saveData);
    }

    public void RestorePayload(string payloadXml)
    {
        var saveData = WorldPersistenceCoordinator.DeserializePayload<RoutinesModuleSaveData>(payloadXml);
        _worldClockService.RestoreSimulation(saveData != null && saveData.HasSimulatedMinuteOfDay ? saveData.SimulatedMinuteOfDay : (int?)null);
    }

    public bool RetryDeferredResolutions() => false;
}
