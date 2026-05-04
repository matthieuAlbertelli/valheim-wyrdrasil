using Wyrdrasil.Core.Tool;
using Wyrdrasil.Registry.Services;
using Wyrdrasil.Settlements.Authoring;

namespace Wyrdrasil.Registry.Services.Interactions;

public sealed class RegistryRuntimeFeedbackService
{
    private readonly ISettlementsAuthoringApi _settlementsAuthoringApi;
    private readonly ConstructionLinkVisualService _constructionLinkVisualService;
    private readonly RegistrySelectionFeedbackService _selectionFeedbackService;

    public RegistryRuntimeFeedbackService(
        ISettlementsAuthoringApi settlementsAuthoringApi,
        ConstructionLinkVisualService constructionLinkVisualService,
        RegistrySelectionFeedbackService selectionFeedbackService)
    {
        _settlementsAuthoringApi = settlementsAuthoringApi;
        _constructionLinkVisualService = constructionLinkVisualService;
        _selectionFeedbackService = selectionFeedbackService;
    }

    public void Update(RegistryToolState state)
    {
        _constructionLinkVisualService.SetVisible(state.SelectedCategory == RegistryCategory.Construction);
        _selectionFeedbackService.Update(state);
        _settlementsAuthoringApi.UpdateTargetedZoneHighlight();
    }

    public void ClearAll()
    {
        _constructionLinkVisualService.SetVisible(false);
        _selectionFeedbackService.ClearAll();
    }
}
