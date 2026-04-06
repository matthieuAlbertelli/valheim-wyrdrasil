using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Actions;

public sealed class DesignateSeatFurnitureAction : IRegistryAction
{
    public RegistryActionType ActionType => RegistryActionType.DesignateSeatFurniture;

    public void Execute(RegistryContext context)
    {
        context.SeatService.DesignateSeatAtCrosshair();
    }
}
