using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Actions;

public sealed class AssignSeatAction : IRegistryAction
{
    public RegistryActionType ActionType => RegistryActionType.AssignSeat;

    public void Execute(RegistryContext context)
    {
        context.ResidentService.AssignSeatAtCrosshair();
    }
}
