using Wyrdrasil.Core.Tool;
using Wyrdrasil.Registry.Actions;

namespace Wyrdrasil.Registry.Handlers;

public sealed class HandlerBackedRegistryAction : IRegistryExecutableAction
{
    private readonly IRegistryActionHandler _handler;

    public HandlerBackedRegistryAction(RegistryActionType actionType, IRegistryActionHandler handler)
    {
        ActionType = actionType;
        _handler = handler;
    }

    public RegistryActionType ActionType { get; }

    public void Execute()
    {
        _handler.Execute();
    }
}
