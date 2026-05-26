using System;

namespace Wyrdrasil.Registry.PlayerTool.WorldObjects;

public sealed class RegistryPlayerToolWorldObjectTarget
{
    public RegistryPlayerToolWorldObjectTarget(
        RegistryPlayerToolWorldObjectTargetKind kind,
        int targetId,
        string displayName,
        object payload)
    {
        Kind = kind;
        TargetId = targetId;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? $"{kind} #{targetId}" : displayName;
        Payload = payload ?? throw new ArgumentNullException(nameof(payload));
    }

    public RegistryPlayerToolWorldObjectTargetKind Kind { get; }
    public int TargetId { get; }
    public string DisplayName { get; }
    public object Payload { get; }
}
