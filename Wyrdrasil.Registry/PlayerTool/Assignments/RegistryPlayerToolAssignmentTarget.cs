using System;

namespace Wyrdrasil.Registry.PlayerTool.Assignments;

public sealed class RegistryPlayerToolAssignmentTarget
{
    public RegistryPlayerToolAssignmentTarget(
        RegistryPlayerToolAssignmentTargetKind kind,
        int targetId,
        string displayName,
        object payload)
    {
        Kind = kind;
        TargetId = targetId;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? $"{kind} #{targetId}" : displayName;
        Payload = payload ?? throw new ArgumentNullException(nameof(payload));
    }

    public RegistryPlayerToolAssignmentTargetKind Kind { get; }
    public int TargetId { get; }
    public string DisplayName { get; }
    public object Payload { get; }
}
