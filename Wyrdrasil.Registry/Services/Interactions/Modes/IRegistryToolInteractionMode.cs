using Wyrdrasil.Core.Tool;

namespace Wyrdrasil.Registry.Services.Interactions.Modes;

public interface IRegistryToolInteractionMode
{
    string Name { get; }

    bool CanHandle(RegistryToolState state);

    void Update(RegistryToolState state, out bool shouldSave);
}
