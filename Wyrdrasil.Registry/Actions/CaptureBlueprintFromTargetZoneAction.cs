using UnityEngine;
using Wyrdrasil.Construction.Authoring;
using Wyrdrasil.Core.Tool;
using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Actions;

public sealed class CaptureBlueprintFromTargetZoneAction : IRegistryAction
{
    public RegistryActionType ActionType => RegistryActionType.CaptureBlueprintFromTargetZone;

    public void Execute(RegistryContext context)
    {
        if (!context.ZoneService.TryGetPlacementPoint(out var point) || !context.ZoneService.TryFindZoneAtPoint(point, out var zone))
        {
            context.Log.LogWarning("Cannot capture construction blueprint: no functional zone was found under the crosshair.");
            return;
        }

        var blueprintId = $"zone.{zone.ZoneType.ToString().ToLowerInvariant()}.{zone.Id}";
        var displayName = $"{zone.ZoneType} Zone #{zone.Id}";
        var request = new ConstructionZoneCaptureRequest
        {
            Zone = zone,
            OriginPosition = zone.Position,
            BlueprintId = blueprintId,
            DisplayName = displayName
        };

        if (!context.ConstructionAuthoringApi.TryCaptureBlueprintFromZone(request, out var blueprint, out var failureReason))
        {
            context.Log.LogWarning($"Failed to capture construction blueprint from zone #{zone.Id}: {failureReason}");
            return;
        }

        context.ConstructionDebugSessionService.SetLatestBlueprintId(blueprint.Id);
        context.Log.LogInfo($"Captured blueprint '{blueprint.DisplayName}' ({blueprint.Id}) from zone #{zone.Id} with {blueprint.Pieces.Count} pieces. Use the Construction placement action to place it elsewhere.");
    }
}
