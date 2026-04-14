using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Actions;

public sealed class DeleteConstructionInTargetZoneAction : IRegistryAction
{
    public RegistryActionType ActionType => RegistryActionType.DeleteConstructionInTargetZone;

    public void Execute(RegistryContext context)
    {
        if (!context.ZoneService.TryGetPlacementPoint(out var placementPoint) ||
            !context.ZoneService.TryFindZoneAtPoint(placementPoint, out var zone))
        {
            context.Log.LogWarning("Aim at a registered functional zone to delete its construction projects.");
            return;
        }

        if (!context.ConstructionTestingApi.TryDeleteConstructionInZone(zone, out var deletedProjectCount, out var destroyedPieceCount, out var failureReason))
        {
            context.Log.LogWarning(failureReason);
            return;
        }

        var clearedResidentCount = context.ResidentService.ClearStaleConstructionAssignments();
        context.Log.LogInfo($"Deleted {deletedProjectCount} construction project(s) in zone #{zone.Id} and destroyed {destroyedPieceCount} built piece instance(s). Cleared {clearedResidentCount} stale resident construction assignment(s).");
    }
}
