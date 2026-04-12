using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Actions;

public sealed class ProbeAssignedCraftStationOccupationAction : IRegistryAction
{
    public RegistryActionType ActionType => RegistryActionType.ProbeAssignedCraftStationOccupation;

    public void Execute(RegistryContext context)
    {
        context.ResidentService.ProbeAssignedCraftStationOccupationAtCrosshair();
    }
}
