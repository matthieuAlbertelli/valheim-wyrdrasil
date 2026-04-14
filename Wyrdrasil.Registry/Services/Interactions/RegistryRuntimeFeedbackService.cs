using Wyrdrasil.Core.Tool;
using Wyrdrasil.Registry.Services;
using Wyrdrasil.Settlements.Services;

namespace Wyrdrasil.Registry.Services.Interactions;

public sealed class RegistryRuntimeFeedbackService
{
    private readonly FunctionalZoneService _zoneService;
    private readonly ConstructionLinkVisualService _constructionLinkVisualService;
    private readonly RegistrySelectionFeedbackService _selectionFeedbackService;

    public RegistryRuntimeFeedbackService(
        FunctionalZoneService zoneService,
        ConstructionLinkVisualService constructionLinkVisualService,
        RegistrySelectionFeedbackService selectionFeedbackService)
    {
        _zoneService = zoneService;
        _constructionLinkVisualService = constructionLinkVisualService;
        _selectionFeedbackService = selectionFeedbackService;
    }

    public void Update(RegistryToolState state)
    {
        _constructionLinkVisualService.SetVisible(state.SelectedCategory == RegistryCategory.Construction);
        _selectionFeedbackService.Update(state);
        _zoneService.UpdateTargetedZoneHighlight();
    }

    public void ClearAll()
    {
        _constructionLinkVisualService.SetVisible(false);
        _selectionFeedbackService.ClearAll();
    }
}
