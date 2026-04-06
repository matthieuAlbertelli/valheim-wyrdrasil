using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Actions;

public sealed class CreateInnkeeperSlotAction : IRegistryAction
{
    public RegistryActionType ActionType => RegistryActionType.CreateInnkeeperSlot;

    public void Execute(RegistryContext context)
    {
        context.SlotService.CreateInnkeeperSlot();
    }
}
