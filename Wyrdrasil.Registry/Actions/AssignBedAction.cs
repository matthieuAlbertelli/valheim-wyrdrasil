using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Actions;

public sealed class AssignBedAction : IRegistryAction
{
    public RegistryActionType ActionType => RegistryActionType.AssignBed;

    public void Execute(RegistryContext context)
    {
        context.ResidentService.AssignBedAtCrosshair();
    }
}
