using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Actions;

public sealed class DesignateCraftStationFurnitureAction : IRegistryAction
{
    public RegistryActionType ActionType => RegistryActionType.DesignateCraftStationFurniture;

    public void Execute(RegistryContext context)
    {
        context.CraftStationService.DesignateCraftStationAtCrosshair();
    }
}
