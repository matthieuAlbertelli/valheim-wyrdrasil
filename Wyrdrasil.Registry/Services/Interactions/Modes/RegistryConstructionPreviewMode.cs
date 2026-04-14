using Wyrdrasil.Core.Tool;
using Wyrdrasil.Registry.Services.Interactions;

namespace Wyrdrasil.Registry.Services.Interactions.Modes;

public sealed class RegistryConstructionPreviewMode : IRegistryToolInteractionMode
{
    private readonly RegistryConstructionPreviewInteractionService _constructionPreviewInteractionService;

    public RegistryConstructionPreviewMode(RegistryConstructionPreviewInteractionService constructionPreviewInteractionService)
    {
        _constructionPreviewInteractionService = constructionPreviewInteractionService;
    }

    public string Name => "ConstructionPreview";

    public bool CanHandle(RegistryToolState state)
    {
        return _constructionPreviewInteractionService.IsPreviewActive;
    }

    public void Update(RegistryToolState state, out bool shouldSave)
    {
        _constructionPreviewInteractionService.Update(out shouldSave);
    }
}
