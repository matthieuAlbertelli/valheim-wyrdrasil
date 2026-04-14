using Wyrdrasil.Core.Tool;
using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Actions;

public sealed class LegacyContextRegistryActionAdapter : IRegistryExecutableAction
{
    private readonly IRegistryAction _legacyAction;
    private readonly RegistryContext _context;

    public LegacyContextRegistryActionAdapter(IRegistryAction legacyAction, RegistryContext context)
    {
        _legacyAction = legacyAction;
        _context = context;
    }

    public RegistryActionType ActionType => _legacyAction.ActionType;

    public void Execute()
    {
        _legacyAction.Execute(_context);
    }
}
