using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Actions;

public sealed class CreateBedroomZoneAction : IRegistryAction
{
    public RegistryActionType ActionType => RegistryActionType.CreateBedroomZone;

    public void Execute(RegistryContext context)
    {
        context.ZoneService.CreateBedroomZone();
    }
}
