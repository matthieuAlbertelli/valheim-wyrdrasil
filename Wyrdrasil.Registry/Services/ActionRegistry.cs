using System.Collections.Generic;
using BepInEx.Logging;
using Wyrdrasil.Registry.Actions;
using Wyrdrasil.Core.Tool;

namespace Wyrdrasil.Registry.Services;

public sealed class ActionRegistry
{
    private readonly ManualLogSource _log;
    private readonly Dictionary<RegistryActionType, IRegistryExecutableAction> _actions = new();

    public ActionRegistry(ManualLogSource log)
    {
        _log = log;
    }

    public void Register(IRegistryExecutableAction action)
    {
        _actions[action.ActionType] = action;
    }


    public void Execute(RegistryActionType actionType)
    {
        if (_actions.TryGetValue(actionType, out var action))
        {
            action.Execute();
            return;
        }

        _log.LogWarning($"No registry action is registered for '{actionType}'.");
    }
}
