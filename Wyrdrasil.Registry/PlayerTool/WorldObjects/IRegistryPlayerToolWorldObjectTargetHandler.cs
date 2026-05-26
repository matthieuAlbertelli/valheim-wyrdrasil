namespace Wyrdrasil.Registry.PlayerTool.WorldObjects;

public interface IRegistryPlayerToolWorldObjectTargetHandler
{
    string TargetKindDisplayName { get; }

    bool CanTargetObjectAtCrosshair();

    bool TryGetExistingTargetAtCrosshair(out RegistryPlayerToolWorldObjectTarget target);

    bool TryResolveOrCreateTargetAtCrosshair(
        out RegistryPlayerToolWorldObjectTarget target,
        out string failureReason);

    bool TryRemoveExistingTargetAtCrosshair(
        out RegistryPlayerToolWorldObjectTarget target,
        out string failureReason);
}
