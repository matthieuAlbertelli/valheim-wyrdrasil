using Wyrdrasil.Core.Tool;
using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Actions;

public sealed class ToggleConstructionVerboseLoggingAction : IRegistryAction
{
    public RegistryActionType ActionType => RegistryActionType.ToggleConstructionVerboseLogging;

    public void Execute(RegistryContext context)
    {
        context.ConstructionTestingApi.ToggleVerboseLogging(out var isEnabled);
        context.Log.LogInfo($"Construction verbose logging {(isEnabled ? "enabled" : "disabled")}.");
    }
}
