using BepInEx.Logging;
using Wyrdrasil.Core.Tool;
using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Actions;

public sealed class LoggingRegistryAction : IRegistryExecutableAction, IRegistryAction
{
    private readonly ManualLogSource? _log;

    public RegistryActionType ActionType { get; }

    public LoggingRegistryAction(RegistryActionType actionType, ManualLogSource? log = null)
    {
        ActionType = actionType;
        _log = log;
    }

    public void Execute()
    {
        _log?.LogInfo($"Executed registry action stub: {ActionType}.");
    }

    public void Execute(RegistryContext context)
    {
        var log = _log ?? context.Log;
        log.LogInfo($"Executed registry action stub: {ActionType}.");
    }
}
