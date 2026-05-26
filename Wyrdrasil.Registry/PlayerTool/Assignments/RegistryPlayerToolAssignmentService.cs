using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using Wyrdrasil.Registry.Services;
using Wyrdrasil.Souls.Tool;
using Wyrdrasil.Registry.PlayerTool;
using Wyrdrasil.Registry.PlayerTool.WorldObjects;

namespace Wyrdrasil.Registry.PlayerTool.Assignments;

public sealed class RegistryPlayerToolAssignmentService
{
    private readonly ManualLogSource _log;
    private readonly RegistryResidentService _residentService;
    private readonly RegistryPlayerToolPieceTableService _pieceTableService;
    private readonly IReadOnlyList<IRegistryPlayerToolAssignmentTargetHandler> _targetHandlers;

    private int? _selectedResidentId;

    public RegistryPlayerToolAssignmentService(
        ManualLogSource log,
        RegistryResidentService residentService,
        RegistryPlayerToolPieceTableService pieceTableService,
        IEnumerable<IRegistryPlayerToolAssignmentTargetHandler> targetHandlers)
    {
        _log = log;
        _residentService = residentService;
        _pieceTableService = pieceTableService;
        _targetHandlers = targetHandlers.ToList();
        RefreshActionDescription();
    }

    public bool HasSelectedResident => _selectedResidentId.HasValue;

    public string CurrentActionDescription
    {
        get
        {
            if (_selectedResidentId.HasValue &&
                _residentService.TryGetResidentById(_selectedResidentId.Value, out var resident))
            {
                return $"Viking sélectionné : {resident.DisplayName}. Cliquez sur un lit, un établi, une forge ou tout autre objet assignable.";
            }

            return RegistryPlayerToolConstants.AssignActionDescription;
        }
    }

    public bool HandlePrimaryActionAtCrosshair()
    {
        if (_residentService.TryGetTargetedRegisteredResident(out var targetedResident))
        {
            SelectResident(targetedResident);
            ShowPlayerMessage($"Registre : {targetedResident.DisplayName} sélectionné. Visez un objet assignable.");
            _log.LogInfo($"Registry assign action selected resident #{targetedResident.Id} ('{targetedResident.DisplayName}').");
            return false;
        }

        if (RegistryPlayerToolWorldTargeting.TryGetTargetCharacter(out _))
        {
            ShowPlayerMessage("Registre : ce viking n'est pas enregistré.");
            _log.LogWarning("Registry assign action blocked: an unregistered viking is targeted, so the object behind it is ignored.");
            return false;
        }

        if (!TryGetSelectedResident(out var selectedResident))
        {
            ClearSelectedResident(silent: true);
            ShowPlayerMessage("Registre : visez d'abord un viking enregistré.");
            _log.LogWarning("Registry assign action failed: no resident is selected.");
            return false;
        }

        if (!TryResolveAssignableTarget(out var handler, out var target, out var failureSummary))
        {
            ShowPlayerMessage("Registre : visez un objet assignable.");
            _log.LogWarning($"Registry assign action failed for resident #{selectedResident.Id}: {failureSummary}");
            return false;
        }

        if (!handler.TryAssign(selectedResident, target, out var playerMessage, out var logMessage))
        {
            ShowPlayerMessage(playerMessage);
            _log.LogWarning($"Registry assign action rejected by {handler.GetType().Name}: {logMessage}");
            return false;
        }

        RefreshActionDescription();
        ShowPlayerMessage(playerMessage);
        _log.LogInfo($"Registry assign action completed by {handler.GetType().Name}: {logMessage}");
        return true;
    }

    public void HandleSecondaryActionAtCrosshair()
    {
        if (CanTargetAssignableObjectAtCrosshair())
        {
            ShowPlayerMessage("Registre : objet assignable ciblé. Visez ailleurs puis clic droit pour annuler la sélection du viking.");
            return;
        }

        if (!HasSelectedResident)
        {
            ShowPlayerMessage("Registre : aucun viking sélectionné.");
            return;
        }

        ClearSelectedResident(silent: false);
    }

    public void ClearSelectedResidentSilently()
    {
        ClearSelectedResident(silent: true);
    }

    private bool TryGetSelectedResident(out RegisteredNpcData resident)
    {
        if (_selectedResidentId.HasValue &&
            _residentService.TryGetResidentById(_selectedResidentId.Value, out resident))
        {
            return true;
        }

        resident = null!;
        return false;
    }

    private void SelectResident(RegisteredNpcData resident)
    {
        _selectedResidentId = resident.Id;
        _residentService.SetPendingForceAssignResidentVisual(resident.Id);
        RefreshActionDescription();
    }

    private void ClearSelectedResident(bool silent)
    {
        if (!_selectedResidentId.HasValue)
        {
            RefreshActionDescription();
            return;
        }

        _selectedResidentId = null;
        _residentService.SetPendingForceAssignResidentVisual(null);
        RefreshActionDescription();

        if (!silent)
        {
            ShowPlayerMessage("Registre : sélection annulée.");
        }
    }

    private bool TryResolveAssignableTarget(
        out IRegistryPlayerToolAssignmentTargetHandler handler,
        out RegistryPlayerToolWorldObjectTarget target,
        out string failureSummary)
    {
        var failureReasons = new List<string>();
        foreach (var candidate in _targetHandlers)
        {
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
            ? "no assignable target handler accepted the crosshair target."
            : string.Join(" | ", failureReasons);
        return false;
    }

    private bool CanTargetAssignableObjectAtCrosshair()
    {
        return _targetHandlers.Any(handler => handler.CanTargetAssignableObjectAtCrosshair());
    }

    public void RefreshActionDescription()
    {
        _pieceTableService.UpdateActionDescription(
            RegistryPlayerToolConstants.AssignActionPiecePrefabName,
            CurrentActionDescription);
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
            _log.LogWarning($"Could not display registry assignment message in HUD: {exception.Message}");
        }
    }
}
