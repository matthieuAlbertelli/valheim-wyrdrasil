using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Actions;

public sealed class InspectTargetNpcAiAction : IRegistryAction
{
    public RegistryActionType ActionType => RegistryActionType.InspectTargetNpcAi;

    public void Execute(RegistryContext context)
    {
        context.DiagnosticsService.InspectTargetNpcAiAtCrosshair();
    }
}
