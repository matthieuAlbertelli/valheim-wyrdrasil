using UnityEngine;
using Wyrdrasil.Construction.Testing;
using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Actions;

public sealed class PlaceBlueprintInstantlyAction : IRegistryAction
{
    public RegistryActionType ActionType => RegistryActionType.PlaceBlueprintInstantly;

    public void Execute(RegistryContext context)
    {
        var player = Player.m_localPlayer;
        if (player == null)
        {
            context.Log.LogWarning("No local player for instant placement.");
            return;
        }

        if (!context.ConstructionDebugSessionService.TryGetLatestBlueprintId(out var blueprintId))
        {
            context.Log.LogWarning("No latest construction blueprint is available for instant placement. Capture a zone blueprint or spawn a test construction project first.");
            return;
        }

        Vector3 originPosition;
        if (context.ZoneService.TryGetPlacementPoint(out var placementPoint))
        {
            originPosition = placementPoint;
            context.Log.LogInfo($"Using registry placement point {originPosition} for blueprint '{blueprintId}'.");
        }
        else
        {
            originPosition = player.transform.position + player.transform.forward * 4f;
            context.Log.LogInfo($"No registry placement point was available. Falling back to forward placement at {originPosition} for blueprint '{blueprintId}'.");
        }

        var request = new PlaceBlueprintInstantRequest
        {
            BlueprintId = blueprintId,
            OriginPosition = originPosition,
            OriginRotation = Quaternion.identity
        };

        if (!context.ConstructionTestingApi.TryPlaceBlueprintInstantly(request, out var count, out var failure))
        {
            context.Log.LogWarning($"Instant placement failed: {failure}");
            return;
        }

        context.Log.LogInfo($"Instantly placed blueprint with {count} pieces.");
    }
}
