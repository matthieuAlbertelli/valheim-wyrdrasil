using System;
using System.Linq;
using BepInEx.Logging;
using UnityEngine;
using Wyrdrasil.Construction.Authoring;
using Wyrdrasil.Registry.Services;
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

    private int? _pendingAssignBedResidentId;

    public RegistryPlayerToolGameplayActionService(
        ManualLogSource log,
        ISettlementsAuthoringApi settlementsAuthoringApi,
        RegistryDeletionService deletionService,
        RegistryResidentService residentService,
        ResidentAssignmentService assignmentService,
        IConstructionAuthoringApi constructionAuthoringApi,
        ConstructionDebugSessionService constructionDebugSessionService)
    {
        _log = log;
        _settlementsAuthoringApi = settlementsAuthoringApi;
        _deletionService = deletionService;
        _residentService = residentService;
        _assignmentService = assignmentService;
        _constructionAuthoringApi = constructionAuthoringApi;
        _constructionDebugSessionService = constructionDebugSessionService;
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


    public bool CaptureTargetedTavernAsBlueprint()
    {
        if (RegistryPlayerToolWorldTargeting.TryGetTargetCharacter(out _))
        {
            ShowPlayerMessage("Registre : visez une taverne, pas un viking.");
            _log.LogWarning("Registry player tavern blueprint capture blocked: a viking is targeted, so the zone behind it is ignored.");
            return false;
        }

        if (!_settlementsAuthoringApi.TryGetPlacementPoint(out var point) ||
            !_settlementsAuthoringApi.TryFindZoneAtPoint(point, out var zone))
        {
            ShowPlayerMessage("Registre : visez une zone de taverne.");
            _log.LogWarning("Registry player tavern blueprint capture failed: no functional zone was found under the crosshair.");
            return false;
        }

        if (zone.ZoneType != ZoneType.Tavern)
        {
            ShowPlayerMessage("Registre : le lieu visé n'est pas une taverne.");
            _log.LogWarning($"Registry player tavern blueprint capture failed: targeted zone #{zone.Id} is {zone.ZoneType}, not Tavern.");
            return false;
        }

        var blueprintId = $"player.tavern.{zone.Id}";
        var displayName = $"Taverne #{zone.Id}";
        var request = new ConstructionZoneCaptureRequest
        {
            Zone = zone,
            OriginPosition = zone.Position,
            BlueprintId = blueprintId,
            DisplayName = displayName
        };

        if (!_constructionAuthoringApi.TryCaptureBlueprintFromZone(request, out var blueprint, out var failureReason))
        {
            ShowPlayerMessage("Registre : impossible d'enregistrer cette taverne.");
            _log.LogWarning($"Registry player tavern blueprint capture failed for zone #{zone.Id}: {failureReason}");
            return false;
        }

        _constructionDebugSessionService.SetLatestBlueprintId(blueprint.Id);
        ShowPlayerMessage($"Registre : modèle '{blueprint.DisplayName}' enregistré ({blueprint.Pieces.Count} pièces).");
        _log.LogInfo($"Registry player action completed: captured tavern blueprint '{blueprint.DisplayName}' ({blueprint.Id}) from zone #{zone.Id} with {blueprint.Pieces.Count} pieces.");
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
        ShowPlayerMessage($"Registre : modèle '{blueprint.DisplayName}' sélectionné.");
        _log.LogInfo($"Registry player blueprint selected: '{blueprint.DisplayName}' ({blueprint.Id}) with {blueprint.Pieces.Count} pieces.");
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
