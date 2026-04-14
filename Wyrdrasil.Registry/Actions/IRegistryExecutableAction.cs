using Wyrdrasil.Core.Tool;

namespace Wyrdrasil.Registry.Actions;

public interface IRegistryExecutableAction
{
    RegistryActionType ActionType { get; }

    void Execute();
}
