using Wyrdrasil.Core.Tool;
using Wyrdrasil.Registry.Services;

namespace Wyrdrasil.Registry.Services.Interactions.Modes;

public sealed class RegistryCraftStationAnchorEditorMode : IRegistryToolInteractionMode
{
    private readonly CraftStationAnchorEditorService _craftStationAnchorEditorService;

    public RegistryCraftStationAnchorEditorMode(CraftStationAnchorEditorService craftStationAnchorEditorService)
    {
        _craftStationAnchorEditorService = craftStationAnchorEditorService;
    }

    public string Name => "CraftStationAnchorEditor";

    public bool CanHandle(RegistryToolState state)
    {
        return _craftStationAnchorEditorService.IsEditing;
    }

    public void Update(RegistryToolState state, out bool shouldSave)
    {
        _craftStationAnchorEditorService.Update(out shouldSave);
    }
}
