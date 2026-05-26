using Wyrdrasil.Registry.Services;
using Wyrdrasil.Settlements.Authoring;
using Wyrdrasil.Settlements.Tool;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Registry.PlayerTool.Assignments;

public sealed class RegistryPlayerToolCraftStationAssignmentTargetHandler : IRegistryPlayerToolAssignmentTargetHandler
{
    private readonly ISettlementsAuthoringApi _settlementsAuthoringApi;
    private readonly ResidentAssignmentService _assignmentService;

    public RegistryPlayerToolCraftStationAssignmentTargetHandler(
        ISettlementsAuthoringApi settlementsAuthoringApi,
        ResidentAssignmentService assignmentService)
    {
        _settlementsAuthoringApi = settlementsAuthoringApi;
        _assignmentService = assignmentService;
    }

    public string TargetKindDisplayName => "poste de travail";

    public bool CanTargetAssignableObjectAtCrosshair()
    {
        return RegistryPlayerToolAssignmentWorldTargeting.HasComponentAtCrosshair<CraftingStation>();
    }

    public bool TryResolveOrCreateTargetAtCrosshair(
        out RegistryPlayerToolAssignmentTarget target,
        out string failureReason)
    {
        if (!_settlementsAuthoringApi.TryGetOrDesignateCraftStationAtCrosshair(out var craftStationData, out failureReason))
        {
            target = null!;
            return false;
        }

        target = new RegistryPlayerToolAssignmentTarget(
            RegistryPlayerToolAssignmentTargetKind.CraftStation,
            craftStationData.Id,
            craftStationData.DisplayName,
            craftStationData);
        failureReason = string.Empty;
        return true;
    }

    public bool TryAssign(
        RegisteredNpcData resident,
        RegistryPlayerToolAssignmentTarget target,
        out string playerMessage,
        out string logMessage)
    {
        if (target.Payload is not RegisteredCraftStationData craftStationData)
        {
            playerMessage = "Registre : cible de poste de travail invalide.";
            logMessage = $"Assignment target payload mismatch: expected RegisteredCraftStationData for target #{target.TargetId}.";
            return false;
        }

        if (!_assignmentService.TryForceAssignToCraftStation(resident, craftStationData))
        {
            playerMessage = "Registre : impossible d'assigner ce poste de travail.";
            logMessage = $"Craft-station assignment rejected: resident #{resident.Id} -> station #{craftStationData.Id}.";
            return false;
        }

        playerMessage = $"Registre : {resident.DisplayName} travaille maintenant au poste #{craftStationData.Id}.";
        logMessage = $"Craft-station assignment completed: resident #{resident.Id} -> station #{craftStationData.Id}.";
        return true;
    }
}
