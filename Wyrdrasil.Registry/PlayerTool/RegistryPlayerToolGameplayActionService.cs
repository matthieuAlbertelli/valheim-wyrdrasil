using System;
using System.Linq;
using BepInEx.Logging;
using UnityEngine;
using Wyrdrasil.Construction.Authoring;
using Wyrdrasil.Registry.Services;
using Wyrdrasil.Registry.Services.Interactions;
using Wyrdrasil.Settlements.Authoring;
using Wyrdrasil.Settlements.Tool;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Registry.PlayerTool;

public sealed class RegistryPlayerToolGameplayActionService
{
    private readonly ManualLogSource _log;
    private readonly ISettlementsAuthoringApi _settlementsAuthoringApi;
    private readonly RegistryDeletionService _deletionService;
    private readonly RegistryResidentService _residentService;
    private readonly ResidentAssignmentService _assignmentService;
    private readonly IConstructionAuthoringApi _constructionAuthoringApi;
    private readonly ConstructionDebugSessionService _constructionDebugSessionService;
    private readonly RegistryConstructionPreviewInteractionService _constructionPreviewInteractionService;

    private int? _pendingAssignBedResidentId;

    public RegistryPlayerToolGameplayActionService(
        ManualLogSource log,
        ISettlementsAuthoringApi settlementsAuthoringApi,
        RegistryDeletionService deletionService,
        RegistryResidentService residentService,
        ResidentAssignmentService assignmentService,
        IConstructionAuthoringApi constructionAuthoringApi,
        ConstructionDebugSessionService constructionDebugSessionService,
        RegistryConstructionPreviewInteractionService constructionPreviewInteractionService)
    {
        _log = log;
        _settlementsAuthoringApi = settlementsAuthoringApi;
        _deletionService = deletionService;
        _residentService = residentService;
        _assignmentService = assignmentService;
        _constructionAuthoringApi = constructionAuthoringApi;
        _constructionDebugSessionService = constructionDebugSessionService;
        _constructionPreviewInteractionService = constructionPreviewInteractionService;
    }

    public bool SpawnAndRegisterViking()
    {
        if (!_residentService.TrySpawnAndRegisterTestViking(out var resident, out var failureReason))
        {
            ShowPlayerMessage("Registre : impossible d'appeler un viking.");
            _log.LogWarning($"Registry player action failed: spawn and register viking. {failureReason}");
            return false;
        }

        ShowPlayerMessage($"Registre : {resident.DisplayName} rejoint le village.");
        _log.LogInfo($"Registry player action completed: spawned and registered resident #{resident.Id} ('{resident.DisplayName}').");
        return true;
    }

    public bool KillTargetedRegisteredViking()
    {
        if (!_residentService.TryKillTargetedRegisteredResident(out var resident, out var failureReason))
        {
            ShowPlayerMessage("Registre : visez un viking enregistré.");
            _log.LogWarning($"Registry player secondary action failed: kill targeted registered viking. {failureReason}");
            return false;
        }

        ClearPendingAssignBedResident();
        ShowPlayerMessage($"Registre : {resident.DisplayName} disparaît du village.");
        _log.LogInfo($"Registry player secondary action completed: killed registered resident #{resident.Id} ('{resident.DisplayName}').");
        return true;
    }

    public bool CreateTavernZoneAtCrosshair()
    {
        if (RegistryPlayerToolWorldTargeting.TryGetTargetCharacter(out _))
        {
            ShowPlayerMessage("Registre : visez le sol ou une construction, pas un viking.");
            return false;
        }

        _settlementsAuthoringApi.CreateTavernZone();
        ShowCurrentTavernAuthoringState("Registre : taverne créée.");
        return true;
    }

    public bool HandleTavernZoneSecondaryInput()
    {
        _settlementsAuthoringApi.HandleZoneAuthoringSecondaryInput();
        ShowCurrentTavernAuthoringState("Registre : tracé de taverne annulé.");
        return false;
    }

    public bool DefineBuildingAtCrosshair()
    {
        if (RegistryPlayerToolWorldTargeting.TryGetTargetCharacter(out _))
        {
            ShowPlayerMessage("Registre : visez le sol ou une construction, pas un viking.");
            return false;
        }

        var finalized = _settlementsAuthoringApi.AdvanceBuildingAuthoring();
        ShowCurrentBuildingAuthoringState(finalized
            ? "Registre : volume de bâtiment créé."
            : "Registre : tracé de bâtiment en cours.");
        return finalized;
    }

    public bool HandleBuildingSecondaryInput()
    {
        _settlementsAuthoringApi.HandleBuildingAuthoringSecondaryInput();
        ShowCurrentBuildingAuthoringState("Registre : tracé de bâtiment annulé.");
        return false;
    }

    public bool DesignateBedAtCrosshair()
    {
        if (RegistryPlayerToolWorldTargeting.TryGetTargetCharacter(out _))
        {
            ShowPlayerMessage("Registre : visez un lit, pas un viking.");
            _log.LogWarning("Registry player bed designation blocked: a viking is targeted, so the construction behind it is ignored.");
            return false;
        }

        if (_settlementsAuthoringApi.TryGetBedAtCrosshair(out var existingBed))
        {
            ShowPlayerMessage($"Registre : lit déjà marqué #{existingBed.Id}.");
            _log.LogInfo($"Registry player action skipped: targeted bed is already designated as bed #{existingBed.Id}.");
            return false;
        }

        _settlementsAuthoringApi.DesignateBedAtCrosshair();

        if (_settlementsAuthoringApi.TryGetBedAtCrosshair(out var designatedBed))
        {
            ShowPlayerMessage($"Registre : lit marqué #{designatedBed.Id}.");
            _log.LogInfo($"Registry player action completed: designated bed #{designatedBed.Id}.");
            return true;
        }

        ShowPlayerMessage("Registre : visez un lit Valheim valide.");
        _log.LogWarning("Registry player action failed: no designated bed could be resolved after designation attempt.");
        return false;
    }

    public bool RemoveDesignatedBedAtCrosshair()
    {
        if (RegistryPlayerToolWorldTargeting.TryGetTargetCharacter(out _))
        {
            ShowPlayerMessage("Registre : visez un lit marqué, pas un viking.");
            _log.LogWarning("Registry player bed removal blocked: a viking is targeted, so the construction behind it is ignored.");
            return false;
        }

        if (!_settlementsAuthoringApi.TryGetBedAtCrosshair(out var bedData))
        {
            ShowPlayerMessage("Registre : visez un lit marqué.");
            _log.LogWarning("Registry player secondary action failed: no designated bed is under the crosshair.");
            return false;
        }

        _deletionService.DeleteDesignatedBedAtCrosshair();
        ShowPlayerMessage($"Registre : lit #{bedData.Id} retiré du Registre.");
        _log.LogInfo($"Registry player secondary action completed: removed designated bed #{bedData.Id}.");
        return true;
    }

    public bool AssignBedAtCrosshair()
    {
        if (_residentService.TryGetTargetedRegisteredResident(out var targetedResident))
        {
            SetPendingAssignBedResident(targetedResident);
            ShowPlayerMessage($"Registre : {targetedResident.DisplayName} sélectionné. Visez un lit marqué.");
            _log.LogInfo($"Registry player bed assignment selected resident #{targetedResident.Id} ('{targetedResident.DisplayName}').");
            return false;
        }

        if (RegistryPlayerToolWorldTargeting.TryGetTargetCharacter(out _))
        {
            ShowPlayerMessage("Registre : ce viking n'est pas enregistré.");
            _log.LogWarning("Registry player bed assignment blocked: an unregistered viking is targeted, so the construction behind it is ignored.");
            return false;
        }

        if (!_pendingAssignBedResidentId.HasValue ||
            !_residentService.TryGetResidentById(_pendingAssignBedResidentId.Value, out var pendingResident))
        {
            ClearPendingAssignBedResident();
            ShowPlayerMessage("Registre : visez d'abord un viking enregistré.");
            _log.LogWarning("Registry player bed assignment failed: no pending resident is selected.");
            return false;
        }

        if (!_settlementsAuthoringApi.TryGetBedAtCrosshair(out var bedData))
        {
            ShowPlayerMessage("Registre : visez un lit marqué.");
            _log.LogWarning($"Registry player bed assignment failed for resident #{pendingResident.Id}: no designated bed targeted.");
            return false;
        }

        if (!_assignmentService.TryForceAssignToBed(pendingResident, bedData))
        {
            ShowPlayerMessage("Registre : impossible d'assigner ce lit.");
            _log.LogWarning($"Registry player bed assignment rejected: resident #{pendingResident.Id} -> bed #{bedData.Id}.");
            return false;
        }

        ClearPendingAssignBedResident();
        ShowPlayerMessage($"Registre : {pendingResident.DisplayName} dort maintenant dans le lit #{bedData.Id}.");
        _log.LogInfo($"Registry player bed assignment completed: resident #{pendingResident.Id} -> bed #{bedData.Id}.");
        return true;
    }


    public bool CaptureTargetedBuildingAsBlueprint()
    {
        if (RegistryPlayerToolWorldTargeting.TryGetTargetCharacter(out _))
        {
            ShowPlayerMessage("Registre : visez un bâtiment, pas un viking.");
            _log.LogWarning("Registry player building blueprint capture blocked: a viking is targeted, so the building behind it is ignored.");
            return false;
        }

        if (!_settlementsAuthoringApi.TryGetBuildingAtCrosshair(out var building))
        {
            ShowPlayerMessage("Registre : visez un volume de bâtiment.");
            _log.LogWarning("Registry player building blueprint capture failed: no building volume was found under the crosshair.");
            return false;
        }

        if (!building.HasVolume)
        {
            ShowPlayerMessage("Registre : ce bâtiment n'a pas de volume de capture.");
            _log.LogWarning($"Registry player building blueprint capture failed: building #{building.Id} has no capture volume.");
            return false;
        }

        var blueprintId = $"player.building.{building.Id}";
        var displayName = string.IsNullOrWhiteSpace(building.DisplayName)
            ? $"Bâtiment #{building.Id}"
            : building.DisplayName;
        var request = new ConstructionBuildingCaptureRequest
        {
            Building = building,
            OriginPosition = building.AnchorPosition,
            BlueprintId = blueprintId,
            DisplayName = displayName
        };

        if (!_constructionAuthoringApi.TryCaptureBlueprintFromBuilding(request, out var blueprint, out var failureReason))
        {
            ShowPlayerMessage("Registre : impossible d'enregistrer ce bâtiment.");
            _log.LogWarning($"Registry player building blueprint capture failed for building #{building.Id}: {failureReason}");
            return false;
        }

        _constructionDebugSessionService.SetLatestBlueprintId(blueprint.Id);
        ShowPlayerMessage($"Registre : modèle '{blueprint.DisplayName}' enregistré ({blueprint.Pieces.Count} pièces).");
        _log.LogInfo($"Registry player action completed: captured building blueprint '{blueprint.DisplayName}' ({blueprint.Id}) from building #{building.Id} with {blueprint.Pieces.Count} pieces.");
        return true;
    }


    public bool SelectBlueprintPlan(string actionPieceName)
    {
        var blueprint = _constructionAuthoringApi.GetBlueprints().FirstOrDefault(candidate =>
            string.Equals(
                RegistryPlayerToolActionDefinitions.CreateBlueprintPlanActionPiecePrefabName(candidate.Id),
                actionPieceName,
                StringComparison.OrdinalIgnoreCase));

        if (blueprint == null)
        {
            ShowPlayerMessage("Registre : modèle introuvable.");
            _log.LogWarning($"Registry player blueprint selection failed: no blueprint matches action piece '{actionPieceName}'.");
            return false;
        }

        _constructionDebugSessionService.SetLatestBlueprintId(blueprint.Id);

        if (!_constructionPreviewInteractionService.TryBeginPreview(blueprint.Id, out var failureReason))
        {
            ShowPlayerMessage("Registre : impossible de prévisualiser ce plan.");
            _log.LogWarning($"Registry player blueprint preview failed for '{blueprint.DisplayName}' ({blueprint.Id}): {failureReason}");
            return false;
        }

        ShowPlayerMessage($"Registre : placez le chantier '{blueprint.DisplayName}'.");
        _log.LogInfo($"Registry player blueprint selected for placement: '{blueprint.DisplayName}' ({blueprint.Id}) with {blueprint.Pieces.Count} pieces.");
        return false;
    }

    public void ClearPendingSubject()
    {
        if (!_pendingAssignBedResidentId.HasValue)
        {
            ShowPlayerMessage("Registre : aucune sélection à annuler.");
            return;
        }

        ClearPendingAssignBedResident();
        ShowPlayerMessage("Registre : sélection annulée.");
    }

    public void ClearPendingSubjectSilently()
    {
        ClearPendingAssignBedResident();
    }

    private void ShowCurrentTavernAuthoringState(string fallbackMessage)
    {
        var snapshot = _settlementsAuthoringApi.GetPendingZoneAuthoringSnapshot();
        if (snapshot == null)
        {
            ShowPlayerMessage(fallbackMessage);
            _log.LogInfo("Registry player action completed: tavern zone authoring state changed.");
            return;
        }

        ShowPlayerMessage(snapshot.CanCloseFootprint
            ? "Registre : contour prêt. Validez près du premier point, puis ajustez la hauteur."
            : $"Registre : point de taverne ajouté ({snapshot.PointCount}).");
    }

    private void ShowCurrentBuildingAuthoringState(string fallbackMessage)
    {
        var snapshot = _settlementsAuthoringApi.GetPendingBuildingAuthoringSnapshot();
        if (snapshot == null)
        {
            ShowPlayerMessage(fallbackMessage);
            _log.LogInfo("Registry player action completed: building authoring state changed.");
            return;
        }

        if (snapshot.Phase == ZoneAuthoringPhase.Height)
        {
            ShowPlayerMessage("Registre : volume prêt. Ajustez la hauteur avec la molette, puis validez.");
            return;
        }

        ShowPlayerMessage(snapshot.CanCloseFootprint
            ? "Registre : contour du bâtiment prêt. Validez près du premier point, puis ajustez la hauteur."
            : $"Registre : point de bâtiment ajouté ({snapshot.PointCount}).");
    }

    private void SetPendingAssignBedResident(RegisteredNpcData resident)
    {
        _pendingAssignBedResidentId = resident.Id;
        _residentService.SetPendingForceAssignResidentVisual(resident.Id);
    }

    private void ClearPendingAssignBedResident()
    {
        _pendingAssignBedResidentId = null;
        _residentService.SetPendingForceAssignResidentVisual(null);
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
            _log.LogWarning($"Could not display registry player tool message in HUD: {exception.Message}");
        }
    }
}
