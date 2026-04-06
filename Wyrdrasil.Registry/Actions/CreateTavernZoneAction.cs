using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Actions;

public sealed class CreateTavernZoneAction : IRegistryAction
{
    public RegistryActionType ActionType => RegistryActionType.CreateTavernZone;

    public void Execute(RegistryContext context)
    {
        context.ZoneService.CreateTavernZone();
    }
}
