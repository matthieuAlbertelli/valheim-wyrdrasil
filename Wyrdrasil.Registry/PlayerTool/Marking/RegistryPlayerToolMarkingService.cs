using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using Wyrdrasil.Registry.PlayerTool.WorldObjects;

namespace Wyrdrasil.Registry.PlayerTool.Marking;

public sealed class RegistryPlayerToolMarkingService
{
    private readonly ManualLogSource _log;
    private readonly RegistryPlayerToolPieceTableService _pieceTableService;
    private readonly IReadOnlyList<IRegistryPlayerToolWorldObjectTargetHandler> _targetHandlers;

    public RegistryPlayerToolMarkingService(
        ManualLogSource log,
        RegistryPlayerToolPieceTableService pieceTableService,
        IEnumerable<IRegistryPlayerToolWorldObjectTargetHandler> targetHandlers)
    {
        _log = log;
        _pieceTableService = pieceTableService;
        _targetHandlers = targetHandlers.ToList();
        RefreshActionDescription();
    }

    public void RefreshActionDescription()
    {
        _pieceTableService.UpdateActionDescription(
            RegistryPlayerToolConstants.MarkActionPiecePrefabName,
            RegistryPlayerToolConstants.MarkActionDescription);
    }

    public bool HandlePrimaryActionAtCrosshair()
    {
        if (RegistryPlayerToolWorldTargeting.TryGetTargetCharacter(out _))
        {
            ShowPlayerMessage("Registre : visez un objet, pas un viking.");
            _log.LogWarning("Registry mark action blocked: a viking is targeted, so the object behind it is ignored.");
            return false;
        }

        if (TryGetExistingMarkedTarget(out var existingHandler, out var existingTarget))
        {
            ShowPlayerMessage($"Registre : {existingHandler.TargetKindDisplayName} déjà marqué #{existingTarget.TargetId}.");
            _log.LogInfo($"Registry mark action skipped: targeted {existingHandler.TargetKindDisplayName} is already registered as #{existingTarget.TargetId}.");
            return false;
        }

        if (!TryResolveOrCreateTarget(out var handler, out var target, out var failureSummary))
        {
            ShowPlayerMessage("Registre : visez un lit, un établi, une forge ou tout autre objet marquable.");
            _log.LogWarning($"Registry mark action failed: {failureSummary}");
            return false;
        }

        ShowPlayerMessage($"Registre : {handler.TargetKindDisplayName} marqué #{target.TargetId}.");
        _log.LogInfo($"Registry mark action completed: registered {handler.TargetKindDisplayName} #{target.TargetId} ('{target.DisplayName}').");
        return true;
    }

    public bool HandleSecondaryActionAtCrosshair()
    {
        if (RegistryPlayerToolWorldTargeting.TryGetTargetCharacter(out _))
        {
            ShowPlayerMessage("Registre : visez un objet marqué, pas un viking.");
            _log.LogWarning("Registry mark removal blocked: a viking is targeted, so the object behind it is ignored.");
            return false;
        }

        if (!TryRemoveMarkedTarget(out var handler, out var target, out var failureSummary))
        {
            ShowPlayerMessage("Registre : visez un objet marqué.");
            _log.LogWarning($"Registry mark removal failed: {failureSummary}");
            return false;
        }

        ShowPlayerMessage($"Registre : {handler.TargetKindDisplayName} #{target.TargetId} retiré du Registre.");
        _log.LogInfo($"Registry mark removal completed: removed {handler.TargetKindDisplayName} #{target.TargetId} ('{target.DisplayName}').");
        return true;
    }

    private bool TryGetExistingMarkedTarget(
        out IRegistryPlayerToolWorldObjectTargetHandler handler,
        out RegistryPlayerToolWorldObjectTarget target)
    {
        foreach (var candidate in _targetHandlers)
        {
            if (candidate.TryGetExistingTargetAtCrosshair(out target))
            {
                handler = candidate;
                return true;
            }
        }

        handler = null!;
        target = null!;
        return false;
    }

    private bool TryResolveOrCreateTarget(
        out IRegistryPlayerToolWorldObjectTargetHandler handler,
        out RegistryPlayerToolWorldObjectTarget target,
        out string failureSummary)
    {
        var failureReasons = new List<string>();
        foreach (var candidate in _targetHandlers)
        {
            if (!candidate.CanTargetObjectAtCrosshair())
            {
                continue;
            }

            if (candidate.TryResolveOrCreateTargetAtCrosshair(out target, out var failureReason))
            {
                handler = candidate;
                failureSummary = string.Empty;
                return true;
            }

            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                failureReasons.Add($"{candidate.TargetKindDisplayName}: {failureReason}");
            }
        }

        handler = null!;
        target = null!;
        failureSummary = failureReasons.Count == 0
            ? "no markable target handler accepted the crosshair target."
            : string.Join(" | ", failureReasons.ToArray());
        return false;
    }

    private bool TryRemoveMarkedTarget(
        out IRegistryPlayerToolWorldObjectTargetHandler handler,
        out RegistryPlayerToolWorldObjectTarget target,
        out string failureSummary)
    {
        var failureReasons = new List<string>();
        foreach (var candidate in _targetHandlers)
        {
            if (candidate.TryRemoveExistingTargetAtCrosshair(out target, out var failureReason))
            {
                handler = candidate;
                failureSummary = string.Empty;
                return true;
            }

            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                failureReasons.Add($"{candidate.TargetKindDisplayName}: {failureReason}");
            }
        }

        handler = null!;
        target = null!;
        failureSummary = failureReasons.Count == 0
            ? "no marked target handler accepted the crosshair target."
            : string.Join(" | ", failureReasons.ToArray());
        return false;
    }

    private void ShowPlayerMessage(string message)
    {
        var player = Player.m_localPlayer;
        if (player == null)
        {
            return;
        }

        try
        {
            player.Message(MessageHud.MessageType.Center, message, 0, null);
        }
        catch (Exception exception)
        {
            _log.LogWarning($"Could not display registry marking message in HUD: {exception.Message}");
        }
    }
}
