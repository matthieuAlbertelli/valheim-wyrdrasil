using System.Collections.Generic;
using Wyrdrasil.Registry.Actions;
using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Services;

public sealed class RegistryActionRegistry
{
    private readonly Dictionary<RegistryActionType, IRegistryAction> _actions = new();

    public void Register(IRegistryAction action)
    {
        _actions[action.ActionType] = action;
    }

    public void Execute(RegistryActionType actionType, RegistryContext context)
    {
        if (_actions.TryGetValue(actionType, out var action))
        {
            action.Execute(context);
            return;
        }

        context.Log.LogWarning($"No registry action is registered for '{actionType}'.");
    }
}
