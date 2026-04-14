using Wyrdrasil.Registry.Services;

namespace Wyrdrasil.Registry.Services.Interactions;

public sealed class RegistryInteractionSessionCoordinator
{
    private readonly RegistryConstructionPreviewInteractionService _constructionPreviewInteractionService;
    private readonly RegistryZoneAuthoringInteractionService _zoneAuthoringInteractionService;
    private readonly CraftStationAnchorEditorService _craftStationAnchorEditorService;

    public RegistryInteractionSessionCoordinator(
        RegistryConstructionPreviewInteractionService constructionPreviewInteractionService,
        RegistryZoneAuthoringInteractionService zoneAuthoringInteractionService,
        CraftStationAnchorEditorService craftStationAnchorEditorService)
    {
        _constructionPreviewInteractionService = constructionPreviewInteractionService;
        _zoneAuthoringInteractionService = zoneAuthoringInteractionService;
        _craftStationAnchorEditorService = craftStationAnchorEditorService;
    }

    public void CancelInteractiveAuthoring()
    {
        if (_constructionPreviewInteractionService.IsPreviewActive)
        {
            _constructionPreviewInteractionService.CancelPreview();
        }

        if (_craftStationAnchorEditorService.IsEditing)
        {
            _craftStationAnchorEditorService.CancelEditing();
        }

        _zoneAuthoringInteractionService.CancelIfActive();
    }
}
