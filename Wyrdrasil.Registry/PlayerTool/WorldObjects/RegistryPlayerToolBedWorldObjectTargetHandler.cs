using Wyrdrasil.Registry.Services;
using Wyrdrasil.Settlements.Authoring;
using Wyrdrasil.Settlements.Tool;

namespace Wyrdrasil.Registry.PlayerTool.WorldObjects;

public sealed class RegistryPlayerToolBedWorldObjectTargetHandler : IRegistryPlayerToolWorldObjectTargetHandler
{
    private readonly ISettlementsAuthoringApi _settlementsAuthoringApi;
    private readonly RegistryDeletionService _deletionService;

    public RegistryPlayerToolBedWorldObjectTargetHandler(
        ISettlementsAuthoringApi settlementsAuthoringApi,
        RegistryDeletionService deletionService)
    {
        _settlementsAuthoringApi = settlementsAuthoringApi;
        _deletionService = deletionService;
    }

    public string TargetKindDisplayName => "lit";

    public bool CanTargetObjectAtCrosshair()
    {
        return RegistryPlayerToolWorldObjectTargeting.HasComponentAtCrosshair<Bed>();
    }

    public bool TryGetExistingTargetAtCrosshair(out RegistryPlayerToolWorldObjectTarget target)
    {
        if (!_settlementsAuthoringApi.TryGetBedAtCrosshair(out var bedData))
        {
            target = null!;
            return false;
        }

        target = CreateTarget(bedData);
        return true;
    }

    public bool TryResolveOrCreateTargetAtCrosshair(
        out RegistryPlayerToolWorldObjectTarget target,
        out string failureReason)
    {
        if (!_settlementsAuthoringApi.TryGetOrDesignateBedAtCrosshair(out var bedData, out failureReason))
        {
            target = null!;
            return false;
        }

        target = CreateTarget(bedData);
        failureReason = string.Empty;
        return true;
    }

    public bool TryRemoveExistingTargetAtCrosshair(
        out RegistryPlayerToolWorldObjectTarget target,
        out string failureReason)
    {
        if (!TryGetExistingTargetAtCrosshair(out target))
        {
            failureReason = "no registered bed is under the crosshair.";
            return false;
        }

        _deletionService.DeleteDesignatedBedAtCrosshair();
        failureReason = string.Empty;
        return true;
    }

    private static RegistryPlayerToolWorldObjectTarget CreateTarget(RegisteredBedData bedData)
    {
        return new RegistryPlayerToolWorldObjectTarget(
            RegistryPlayerToolWorldObjectTargetKind.Bed,
            bedData.Id,
            bedData.DisplayName,
            bedData);
    }
}
