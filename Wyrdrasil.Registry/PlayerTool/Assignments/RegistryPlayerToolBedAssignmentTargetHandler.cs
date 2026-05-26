using Wyrdrasil.Registry.Services;
using Wyrdrasil.Settlements.Authoring;
using Wyrdrasil.Settlements.Tool;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Registry.PlayerTool.Assignments;

public sealed class RegistryPlayerToolBedAssignmentTargetHandler : IRegistryPlayerToolAssignmentTargetHandler
{
    private readonly ISettlementsAuthoringApi _settlementsAuthoringApi;
    private readonly ResidentAssignmentService _assignmentService;

    public RegistryPlayerToolBedAssignmentTargetHandler(
        ISettlementsAuthoringApi settlementsAuthoringApi,
        ResidentAssignmentService assignmentService)
    {
        _settlementsAuthoringApi = settlementsAuthoringApi;
        _assignmentService = assignmentService;
    }

    public string TargetKindDisplayName => "lit";

    public bool CanTargetAssignableObjectAtCrosshair()
    {
        return RegistryPlayerToolAssignmentWorldTargeting.HasComponentAtCrosshair<Bed>();
    }

    public bool TryResolveOrCreateTargetAtCrosshair(
        out RegistryPlayerToolAssignmentTarget target,
        out string failureReason)
    {
        if (!_settlementsAuthoringApi.TryGetOrDesignateBedAtCrosshair(out var bedData, out failureReason))
        {
            target = null!;
            return false;
        }

        target = new RegistryPlayerToolAssignmentTarget(
            RegistryPlayerToolAssignmentTargetKind.Bed,
            bedData.Id,
            bedData.DisplayName,
            bedData);
        failureReason = string.Empty;
        return true;
    }

    public bool TryAssign(
        RegisteredNpcData resident,
        RegistryPlayerToolAssignmentTarget target,
        out string playerMessage,
        out string logMessage)
    {
        if (target.Payload is not RegisteredBedData bedData)
        {
            playerMessage = "Registre : cible de lit invalide.";
            logMessage = $"Assignment target payload mismatch: expected RegisteredBedData for target #{target.TargetId}.";
            return false;
        }

        if (!_assignmentService.TryForceAssignToBed(resident, bedData))
        {
            playerMessage = "Registre : impossible d'assigner ce lit.";
            logMessage = $"Bed assignment rejected: resident #{resident.Id} -> bed #{bedData.Id}.";
            return false;
        }

        playerMessage = $"Registre : {resident.DisplayName} dort maintenant dans le lit #{bedData.Id}.";
        logMessage = $"Bed assignment completed: resident #{resident.Id} -> bed #{bedData.Id}.";
        return true;
    }
}
