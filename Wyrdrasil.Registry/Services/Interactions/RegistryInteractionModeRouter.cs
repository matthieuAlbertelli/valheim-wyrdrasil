using System.Collections.Generic;
using Wyrdrasil.Core.Tool;
using Wyrdrasil.Registry.Services.Interactions.Modes;

namespace Wyrdrasil.Registry.Services.Interactions;

public sealed class RegistryInteractionModeRouter
{
    private readonly IReadOnlyList<IRegistryToolInteractionMode> _modes;
    private readonly IRegistryToolInteractionMode _fallbackMode;

    public RegistryInteractionModeRouter(
        IReadOnlyList<IRegistryToolInteractionMode> modes,
        IRegistryToolInteractionMode fallbackMode)
    {
        _modes = modes;
        _fallbackMode = fallbackMode;
    }

    public string ResolveModeName(RegistryToolState state)
    {
        return ResolveMode(state).Name;
    }

    public void Update(RegistryToolState state, out bool shouldSave)
    {
        ResolveMode(state).Update(state, out shouldSave);
    }

    private IRegistryToolInteractionMode ResolveMode(RegistryToolState state)
    {
        foreach (var mode in _modes)
        {
            if (mode.CanHandle(state))
            {
                return mode;
            }
        }

        return _fallbackMode;
    }
}
