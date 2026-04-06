using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Actions;

public interface IRegistryAction
{
    RegistryActionType ActionType { get; }

    void Execute(RegistryContext context);
}
