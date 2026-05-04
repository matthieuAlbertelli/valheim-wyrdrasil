using Wyrdrasil.Registry.Services;
using Wyrdrasil.Souls.Authoring;

namespace Wyrdrasil.Registry.Handlers;

public sealed class SpawnTestVikingHandler : IRegistryActionHandler
{
    private readonly ISoulsAuthoringApi _soulsAuthoringApi;

    public SpawnTestVikingHandler(ISoulsAuthoringApi soulsAuthoringApi)
    {
        _soulsAuthoringApi = soulsAuthoringApi;
    }

    public void Execute()
    {
        _soulsAuthoringApi.SpawnTestViking();
    }
}

public sealed class InspectTargetNpcAiHandler : IRegistryActionHandler
{
    private readonly TargetDiagnosticsService _diagnosticsService;

    public InspectTargetNpcAiHandler(TargetDiagnosticsService diagnosticsService)
    {
        _diagnosticsService = diagnosticsService;
    }

    public void Execute()
    {
        _diagnosticsService.InspectTargetNpcAiAtCrosshair();
    }
}

public sealed class EditTargetCraftStationAnchorHandler : IRegistryActionHandler
{
    private readonly CraftStationAnchorEditorService _craftStationAnchorEditorService;

    public EditTargetCraftStationAnchorHandler(CraftStationAnchorEditorService craftStationAnchorEditorService)
    {
        _craftStationAnchorEditorService = craftStationAnchorEditorService;
    }

    public void Execute()
    {
        _craftStationAnchorEditorService.BeginEditingTargetedCraftStation();
    }
}

public sealed class DeleteConstructionInTargetZoneHandler : IRegistryActionHandler
{
    private readonly RegistryDeletionService _deletionService;

    public DeleteConstructionInTargetZoneHandler(RegistryDeletionService deletionService)
    {
        _deletionService = deletionService;
    }

    public void Execute()
    {
        _deletionService.PurgeAllConstructionInTargetZone();
    }
}

public sealed class FlushRegistryStateHandler : IRegistryActionHandler
{
    private readonly RegistryFlushService _flushService;

    public FlushRegistryStateHandler(RegistryFlushService flushService)
    {
        _flushService = flushService;
    }

    public void Execute()
    {
        _flushService.FlushAllRegistryState();
    }
}
