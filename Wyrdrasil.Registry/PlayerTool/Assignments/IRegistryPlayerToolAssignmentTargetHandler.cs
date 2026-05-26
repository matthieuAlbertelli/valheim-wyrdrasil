using Wyrdrasil.Registry.PlayerTool.WorldObjects;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Registry.PlayerTool.Assignments;

public interface IRegistryPlayerToolAssignmentTargetHandler
{
    string TargetKindDisplayName { get; }

    bool CanTargetAssignableObjectAtCrosshair();

    bool TryResolveOrCreateTargetAtCrosshair(
        out RegistryPlayerToolWorldObjectTarget target,
        out string failureReason);

    bool TryAssign(
        RegisteredNpcData resident,
        RegistryPlayerToolWorldObjectTarget target,
        out string playerMessage,
        out string logMessage);
}
