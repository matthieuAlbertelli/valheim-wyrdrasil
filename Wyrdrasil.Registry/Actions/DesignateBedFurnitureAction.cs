using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Actions;

public sealed class DesignateBedFurnitureAction : IRegistryAction
{
    public RegistryActionType ActionType => RegistryActionType.DesignateBedFurniture;

    public void Execute(RegistryContext context)
    {
        context.BedService.DesignateBedAtCrosshair();
    }
}
