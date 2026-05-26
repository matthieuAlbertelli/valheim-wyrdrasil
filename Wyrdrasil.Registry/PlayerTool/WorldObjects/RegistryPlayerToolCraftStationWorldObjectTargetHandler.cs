using Wyrdrasil.Registry.Services;
using Wyrdrasil.Settlements.Authoring;
using Wyrdrasil.Settlements.Tool;

namespace Wyrdrasil.Registry.PlayerTool.WorldObjects;

public sealed class RegistryPlayerToolCraftStationWorldObjectTargetHandler : IRegistryPlayerToolWorldObjectTargetHandler
{
    private readonly ISettlementsAuthoringApi _settlementsAuthoringApi;
    private readonly RegistryDeletionService _deletionService;

    public RegistryPlayerToolCraftStationWorldObjectTargetHandler(
        ISettlementsAuthoringApi settlementsAuthoringApi,
        RegistryDeletionService deletionService)
    {
        _settlementsAuthoringApi = settlementsAuthoringApi;
        _deletionService = deletionService;
    }

    public string TargetKindDisplayName => "poste de travail";

    public bool CanTargetObjectAtCrosshair()
    {
        return RegistryPlayerToolWorldObjectTargeting.HasComponentAtCrosshair<CraftingStation>();
    }

    public bool TryGetExistingTargetAtCrosshair(out RegistryPlayerToolWorldObjectTarget target)
    {
        if (!_settlementsAuthoringApi.TryGetCraftStationAtCrosshair(out var craftStationData))
        {
            target = null!;
            return false;
        }

        target = CreateTarget(craftStationData);
        return true;
    }

    public bool TryResolveOrCreateTargetAtCrosshair(
        out RegistryPlayerToolWorldObjectTarget target,
        out string failureReason)
    {
        if (!_settlementsAuthoringApi.TryGetOrDesignateCraftStationAtCrosshair(out var craftStationData, out failureReason))
        {
            target = null!;
            return false;
        }

        target = CreateTarget(craftStationData);
        failureReason = string.Empty;
        return true;
    }

    public bool TryRemoveExistingTargetAtCrosshair(
        out RegistryPlayerToolWorldObjectTarget target,
        out string failureReason)
    {
        if (!TryGetExistingTargetAtCrosshair(out target))
        {
            failureReason = "no registered craft station is under the crosshair.";
            return false;
        }

        _deletionService.DeleteDesignatedCraftStationAtCrosshair();
        failureReason = string.Empty;
        return true;
    }

    private static RegistryPlayerToolWorldObjectTarget CreateTarget(RegisteredCraftStationData craftStationData)
    {
        return new RegistryPlayerToolWorldObjectTarget(
            RegistryPlayerToolWorldObjectTargetKind.CraftStation,
            craftStationData.Id,
            craftStationData.DisplayName,
            craftStationData);
    }
}
