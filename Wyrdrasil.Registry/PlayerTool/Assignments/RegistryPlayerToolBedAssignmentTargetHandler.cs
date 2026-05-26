using Wyrdrasil.Registry.Services;
using Wyrdrasil.Registry.PlayerTool.WorldObjects;
using Wyrdrasil.Settlements.Tool;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Registry.PlayerTool.Assignments;

public sealed class RegistryPlayerToolBedAssignmentTargetHandler : IRegistryPlayerToolAssignmentTargetHandler
{
    private readonly IRegistryPlayerToolWorldObjectTargetHandler _targetHandler;
    private readonly ResidentAssignmentService _assignmentService;

    public RegistryPlayerToolBedAssignmentTargetHandler(
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
