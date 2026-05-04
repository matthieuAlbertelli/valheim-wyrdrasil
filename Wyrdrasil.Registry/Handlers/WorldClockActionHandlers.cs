using Wyrdrasil.Routines.Runtime;

namespace Wyrdrasil.Registry.Handlers;

public sealed class SimulateNoonHandler : IRegistryActionHandler
{
    private readonly IRoutinesRuntimeApi _routinesRuntimeApi;

    public SimulateNoonHandler(IRoutinesRuntimeApi routinesRuntimeApi)
    {
        _routinesRuntimeApi = routinesRuntimeApi;
    }

    public void Execute()
    {
        _routinesRuntimeApi.SimulateNoon();
    }
}

public sealed class SimulateNightHandler : IRegistryActionHandler
{
    private readonly IRoutinesRuntimeApi _routinesRuntimeApi;

    public SimulateNightHandler(IRoutinesRuntimeApi routinesRuntimeApi)
    {
        _routinesRuntimeApi = routinesRuntimeApi;
    }

    public void Execute()
    {
        _routinesRuntimeApi.SimulateNight();
    }
}

public sealed class ClearTimeSimulationHandler : IRegistryActionHandler
{
    private readonly IRoutinesRuntimeApi _routinesRuntimeApi;

    public ClearTimeSimulationHandler(IRoutinesRuntimeApi routinesRuntimeApi)
    {
        _routinesRuntimeApi = routinesRuntimeApi;
    }

    public void Execute()
    {
        _routinesRuntimeApi.ClearTimeSimulation();
    }
}
