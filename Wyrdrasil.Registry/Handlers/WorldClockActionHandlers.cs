using Wyrdrasil.Routines.Services;

namespace Wyrdrasil.Registry.Handlers;

public sealed class SimulateNoonHandler : IRegistryActionHandler
{
    private readonly WorldClockService _worldClockService;

    public SimulateNoonHandler(WorldClockService worldClockService)
    {
        _worldClockService = worldClockService;
    }

    public void Execute()
    {
        _worldClockService.SimulateNoon();
    }
}

public sealed class SimulateNightHandler : IRegistryActionHandler
{
    private readonly WorldClockService _worldClockService;

    public SimulateNightHandler(WorldClockService worldClockService)
    {
        _worldClockService = worldClockService;
    }

    public void Execute()
    {
        _worldClockService.SimulateNight();
    }
}

public sealed class ClearTimeSimulationHandler : IRegistryActionHandler
{
    private readonly WorldClockService _worldClockService;

    public ClearTimeSimulationHandler(WorldClockService worldClockService)
    {
        _worldClockService = worldClockService;
    }

    public void Execute()
    {
        _worldClockService.ClearSimulation();
    }
}
