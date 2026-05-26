using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using Wyrdrasil.Construction.Runtime;
using Wyrdrasil.Construction.Services;
using Wyrdrasil.Registry.Services;
using Wyrdrasil.Settlements.Services;
using Wyrdrasil.Settlements.Tool;
using Wyrdrasil.Souls.Tool;
using Wyrdrasil.Registry.PlayerTool;
using Wyrdrasil.Registry.PlayerTool.WorldObjects;

namespace Wyrdrasil.Registry.PlayerTool.Assignments;

public sealed class RegistryPlayerToolAssignmentService
{
    private readonly ManualLogSource _log;
    private readonly RegistryResidentService _residentService;
    private readonly CraftStationService _craftStationService;
    private readonly IConstructionRuntimeApi _constructionRuntimeApi;
    private readonly ConstructionProjectMarkerService _constructionProjectMarkerService;
    private readonly ConstructionDebugSessionService _constructionDebugSessionService;
    private readonly ConstructionLinkVisualService _constructionLinkVisualService;
    private readonly RegistryPlayerToolPieceTableService _pieceTableService;
    private readonly IReadOnlyList<IRegistryPlayerToolAssignmentTargetHandler> _targetHandlers;

    private int? _selectedResidentId;
    private int? _selectedConstructionProjectId;

    public RegistryPlayerToolAssignmentService(
        ManualLogSource log,
        RegistryResidentService residentService,
        CraftStationService craftStationService,
        IConstructionRuntimeApi constructionRuntimeApi,
        ConstructionProjectMarkerService constructionProjectMarkerService,
        ConstructionDebugSessionService constructionDebugSessionService,
        ConstructionLinkVisualService constructionLinkVisualService,
        RegistryPlayerToolPieceTableService pieceTableService,
        IEnumerable<IRegistryPlayerToolAssignmentTargetHandler> targetHandlers)
    {
        _log = log;
        _residentService = residentService;
        _craftStationService = craftStationService;
        _constructionRuntimeApi = constructionRuntimeApi;
        _constructionProjectMarkerService = constructionProjectMarkerService;
        _constructionDebugSessionService = constructionDebugSessionService;
        _constructionLinkVisualService = constructionLinkVisualService;
        _pieceTableService = pieceTableService;
        _targetHandlers = targetHandlers.ToList();
        RefreshActionDescription();
    }

    public bool HasSelectedResident => _selectedResidentId.HasValue;
    public bool HasSelectedConstructionProject => _selectedConstructionProjectId.HasValue;

    public string CurrentActionDescription
    {
        get
        {
            if (_selectedConstructionProjectId.HasValue)
            {
                return $"Chantier sélectionné : #{_selectedConstructionProjectId.Value}. Cliquez sur un workbench ou un viking enregistré à associer au chantier.";
            }

            if (_selectedResidentId.HasValue &&
                _residentService.TryGetResidentById(_selectedResidentId.Value, out var resident))
            {
                return $"Viking sélectionné : {resident.DisplayName}. Cliquez sur un lit, un établi, une forge, un chantier ou tout autre objet assignable.";
            }

            return RegistryPlayerToolConstants.AssignActionDescription;
        }
    }

    public bool HandlePrimaryActionAtCrosshair()
    {
        if (TryHandleSelectedConstructionProjectAtCrosshair(out var selectedProjectHandled, out var selectedProjectPersistentChange))
        {
            return selectedProjectPersistentChange;
        }

        if (selectedProjectHandled)
        {
            return false;
        }

        if (TryHandleConstructionProjectSelectionOrResidentAssignment(out var constructionProjectHandled, out var constructionProjectPersistentChange))
        {
            return constructionProjectPersistentChange;
        }

        if (constructionProjectHandled)
        {
            return false;
        }

        if (_residentService.TryGetTargetedRegisteredResident(out var targetedResident))
        {
            SelectResident(targetedResident);
            ShowPlayerMessage($"Registre : {targetedResident.DisplayName} sélectionné. Visez un objet assignable ou un chantier.");
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
            ShowPlayerMessage("Registre : visez d'abord un viking enregistré ou un chantier.");
            _log.LogWarning("Registry assign action failed: neither a resident nor a construction project is selected.");
            return false;
        }

        if (!TryResolveAssignableTarget(out var handler, out var target, out var failureSummary))
        {
            ShowPlayerMessage("Registre : visez un objet assignable ou un chantier.");
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
        if (CanTargetAssignableObjectAtCrosshair() || _constructionProjectMarkerService.TryGetTargetedProjectId(out _))
        {
            ShowPlayerMessage("Registre : cible assignable. Visez ailleurs puis clic droit pour annuler la sélection.");
            return;
        }

        if (!HasSelectedResident && !HasSelectedConstructionProject)
        {
            ShowPlayerMessage("Registre : aucune sélection active.");
            return;
        }

        ClearSelection(silent: false);
    }

    public void ClearSelectedResidentSilently()
    {
        ClearSelection(silent: true);
    }

    private bool TryHandleSelectedConstructionProjectAtCrosshair(out bool handled, out bool persistentChange)
    {
        handled = false;
        persistentChange = false;

        if (!_selectedConstructionProjectId.HasValue)
        {
            return false;
        }

        handled = true;
        var projectId = _selectedConstructionProjectId.Value;

        if (_constructionProjectMarkerService.TryGetTargetedProjectId(out var targetedProjectId))
        {
            SelectConstructionProject(targetedProjectId);
            ShowPlayerMessage($"Registre : chantier #{targetedProjectId} sélectionné. Visez un workbench ou un viking.");
            _log.LogInfo($"Registry assign action switched selected construction project to #{targetedProjectId}.");
            return true;
        }

        if (_residentService.TryGetTargetedRegisteredResident(out var targetedResident))
        {
            persistentChange = TryAssignResidentToSelectedConstructionProject(targetedResident, projectId);
            return true;
        }

        if (RegistryPlayerToolWorldTargeting.TryGetTargetCharacter(out _))
        {
            ShowPlayerMessage("Registre : ce viking n'est pas enregistré.");
            _log.LogWarning($"Registry construction assignment blocked for project #{projectId}: targeted character is not a registered resident.");
            return true;
        }

        if (TryResolveCraftStationTarget(out var craftStationData, out var craftStationFailureReason))
        {
            persistentChange = TryAssignCraftStationToSelectedConstructionProject(craftStationData, projectId);
            return true;
        }

        ShowPlayerMessage("Registre : visez un workbench ou un viking enregistré à associer au chantier.");
        _log.LogWarning($"Registry construction assignment failed for project #{projectId}: {craftStationFailureReason}");
        return true;
    }

    private bool TryHandleConstructionProjectSelectionOrResidentAssignment(out bool handled, out bool persistentChange)
    {
        handled = false;
        persistentChange = false;

        if (!_constructionProjectMarkerService.TryGetTargetedProjectId(out var targetedProjectId))
        {
            return false;
        }

        handled = true;

        if (TryGetSelectedResident(out var selectedResident))
        {
            persistentChange = TryAssignResidentToSelectedConstructionProject(selectedResident, targetedProjectId);
            return true;
        }

        SelectConstructionProject(targetedProjectId);
        ShowPlayerMessage($"Registre : chantier #{targetedProjectId} sélectionné. Visez un workbench ou un viking.");
        _log.LogInfo($"Registry assign action selected construction project #{targetedProjectId}.");
        return false;
    }

    private bool TryAssignCraftStationToSelectedConstructionProject(RegisteredCraftStationData craftStationData, int projectId)
    {
        if (craftStationData.AssignedRegisteredNpcId.HasValue)
        {
            ShowPlayerMessage("Registre : ce workbench est déjà assigné à un viking.");
            _log.LogWarning($"Registry construction workbench assignment rejected: craft station #{craftStationData.Id} is already assigned to resident #{craftStationData.AssignedRegisteredNpcId.Value}.");
            return false;
        }

        if (!_constructionRuntimeApi.TryAssignCraftStationToProject(craftStationData.Id, projectId, out var workPost, out var failureReason))
        {
            ShowPlayerMessage("Registre : impossible d'associer ce workbench au chantier.");
            _log.LogWarning($"Registry construction workbench assignment failed: craft station #{craftStationData.Id} -> project #{projectId}. {failureReason}");
            return false;
        }

        _constructionDebugSessionService.SetLatestProjectId(projectId);
        SelectConstructionProject(projectId);
        RefreshConstructionAssignmentFeedback();
        ShowPlayerMessage($"Registre : workbench #{craftStationData.Id} associé au chantier #{projectId}.");
        _log.LogInfo($"Registry construction workbench assignment completed: craft station #{craftStationData.Id} -> project #{projectId}, work post #{workPost.Id}.");
        return true;
    }

    private bool TryAssignResidentToSelectedConstructionProject(RegisteredNpcData resident, int projectId)
    {
        if (!_residentService.TryAssignResidentToConstructionProject(resident, projectId, out var workPostId, out var failureReason))
        {
            ShowPlayerMessage("Registre : impossible d'assigner ce viking au chantier.");
            _log.LogWarning($"Registry construction resident assignment failed: resident #{resident.Id} -> project #{projectId}. {failureReason}");
            return false;
        }

        _constructionDebugSessionService.SetLatestProjectId(projectId);
        SelectConstructionProject(projectId);
        RefreshConstructionAssignmentFeedback();
        ShowPlayerMessage($"Registre : {resident.DisplayName} travaille maintenant sur le chantier #{projectId}.");
        _log.LogInfo($"Registry construction resident assignment completed: resident #{resident.Id} -> project #{projectId}, work post #{workPostId}.");
        return true;
    }

    private bool TryResolveCraftStationTarget(out RegisteredCraftStationData craftStationData, out string failureReason)
    {
        if (TryResolveAssignableTarget(out _, out var target, out failureReason) &&
            target.Payload is RegisteredCraftStationData resolvedCraftStation)
        {
            craftStationData = resolvedCraftStation;
            return true;
        }

        craftStationData = null!;
        if (string.IsNullOrWhiteSpace(failureReason))
        {
            failureReason = "no craft station target was found under the crosshair.";
        }

        return false;
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
        _selectedConstructionProjectId = null;
        _constructionProjectMarkerService.SetHoveredProject(null);
        _constructionLinkVisualService.SetHoveredWorkbenchLink(null, null);
        _constructionLinkVisualService.SetHoveredResidentLink(null, null);
        _craftStationService.SetPendingConstructionTarget(null);
        _residentService.SetPendingConstructionAssignmentResidentVisual(null);
        _residentService.SetPendingForceAssignResidentVisual(resident.Id);
        RefreshActionDescription();
    }

    private void SelectConstructionProject(int projectId)
    {
        _selectedResidentId = null;
        _selectedConstructionProjectId = projectId > 0 ? projectId : null;
        _residentService.SetPendingForceAssignResidentVisual(null);
        _constructionProjectMarkerService.SetHoveredProject(_selectedConstructionProjectId);
        RefreshActionDescription();
    }

    private void ClearSelection(bool silent)
    {
        var hadSelection = _selectedResidentId.HasValue || _selectedConstructionProjectId.HasValue;
        _selectedResidentId = null;
        _selectedConstructionProjectId = null;
        _residentService.SetPendingForceAssignResidentVisual(null);
        _residentService.SetPendingConstructionAssignmentResidentVisual(null);
        _constructionProjectMarkerService.SetHoveredProject(null);
        _craftStationService.SetPendingConstructionTarget(null);
        _constructionLinkVisualService.SetHoveredWorkbenchLink(null, null);
        _constructionLinkVisualService.SetHoveredResidentLink(null, null);
        RefreshActionDescription();

        if (!silent && hadSelection)
        {
            ShowPlayerMessage("Registre : sélection annulée.");
        }
    }

    private void ClearSelectedResident(bool silent)
    {
        ClearSelection(silent);
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
        RefreshConstructionAssignmentFeedback();
        _pieceTableService.UpdateActionDescription(
            RegistryPlayerToolConstants.AssignActionPiecePrefabName,
            CurrentActionDescription);
    }

    private void RefreshConstructionAssignmentFeedback()
    {
        if (!_selectedConstructionProjectId.HasValue)
        {
            return;
        }

        var projectId = _selectedConstructionProjectId.Value;
        _constructionProjectMarkerService.SetHoveredProject(projectId);

        if (_craftStationService.TryGetCraftStationAtCrosshair(out var craftStation))
        {
            _craftStationService.SetPendingConstructionTarget(craftStation.Id);
            _constructionLinkVisualService.SetHoveredWorkbenchLink(projectId, craftStation.Id);
        }
        else
        {
            _craftStationService.SetPendingConstructionTarget(null);
            _constructionLinkVisualService.SetHoveredWorkbenchLink(null, null);
        }

        if (_residentService.TryGetTargetedRegisteredResident(out var resident))
        {
            _residentService.SetPendingConstructionAssignmentResidentVisual(resident.Id);
            _constructionLinkVisualService.SetHoveredResidentLink(projectId, resident.Id);
        }
        else
        {
            _residentService.SetPendingConstructionAssignmentResidentVisual(null);
            _constructionLinkVisualService.SetHoveredResidentLink(null, null);
        }
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
