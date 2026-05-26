using Wyrdrasil.Registry.Services;
using Wyrdrasil.Registry.PlayerTool.WorldObjects;
using Wyrdrasil.Settlements.Tool;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Registry.PlayerTool.Assignments;

public sealed class RegistryPlayerToolCraftStationAssignmentTargetHandler : IRegistryPlayerToolAssignmentTargetHandler
{
    private readonly IRegistryPlayerToolWorldObjectTargetHandler _targetHandler;
    private readonly ResidentAssignmentService _assignmentService;

    public RegistryPlayerToolCraftStationAssignmentTargetHandler(
        IRegistryPlayerToolWorldObjectTargetHandler targetHandler,
        ResidentAssignmentService assignmentService)
    {
        _targetHandler = targetHandler;
        _assignmentService = assignmentService;
    }

    public string TargetKindDisplayName => _targetHandler.TargetKindDisplayName;

    public bool CanTargetAssignableObjectAtCrosshair()
    {
        return _targetHandler.CanTargetObjectAtCrosshair();
    }

    public bool TryResolveOrCreateTargetAtCrosshair(
        out RegistryPlayerToolWorldObjectTarget target,
        out string failureReason)
    {
        return _targetHandler.TryResolveOrCreateTargetAtCrosshair(out target, out failureReason);
    }

    public bool TryAssign(
        RegisteredNpcData resident,
        RegistryPlayerToolWorldObjectTarget target,
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
