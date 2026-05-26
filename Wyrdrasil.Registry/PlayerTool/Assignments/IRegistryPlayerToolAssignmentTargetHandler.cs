using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Registry.PlayerTool.Assignments;

public interface IRegistryPlayerToolAssignmentTargetHandler
{
    string TargetKindDisplayName { get; }

    bool CanTargetAssignableObjectAtCrosshair();

    bool TryResolveOrCreateTargetAtCrosshair(
        out RegistryPlayerToolAssignmentTarget target,
        out string failureReason);

    bool TryAssign(
        RegisteredNpcData resident,
        RegistryPlayerToolAssignmentTarget target,
        out string playerMessage,
        out string logMessage);
}
