using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Actions;

public sealed class EditTargetCraftStationAnchorAction : IRegistryAction
{
    public RegistryActionType ActionType => RegistryActionType.EditTargetCraftStationAnchor;

    public void Execute(RegistryContext context)
    {
        context.CraftStationAnchorEditorService.BeginEditingTargetedCraftStation();
    }
}
