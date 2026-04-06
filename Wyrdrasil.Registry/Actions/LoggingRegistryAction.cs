using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Actions;

public sealed class LoggingRegistryAction : IRegistryAction
{
    public RegistryActionType ActionType { get; }

    public LoggingRegistryAction(RegistryActionType actionType)
    {
        ActionType = actionType;
    }

    public void Execute(RegistryContext context)
    {
        context.Log.LogInfo($"Executed registry action stub: {ActionType}.");
    }
}
