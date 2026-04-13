using UnityEngine;
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
            context.Log.LogWarning("No local player for construction placement preview.");
            return;
        }

        if (!context.ConstructionDebugSessionService.TryGetLatestBlueprintId(out var blueprintId))
        {
            context.Log.LogWarning("No latest construction blueprint is available for preview. Capture a zone blueprint or spawn a test construction project first.");
            return;
        }

        Vector3 originPosition;
        if (context.ZoneService.TryGetPlacementPoint(out var placementPoint))
        {
            originPosition = placementPoint;
        }
        else
        {
            originPosition = player.transform.position + (player.transform.forward * 4f);
        }

        if (!context.ConstructionPlacementPreviewService.TryBeginPreview(
                blueprintId,
                originPosition,
                Quaternion.identity,
                out var failureReason))
        {
            context.Log.LogWarning($"Failed to start construction placement preview: {failureReason}");
            return;
        }

        context.Log.LogInfo($"Started construction placement preview for blueprint '{blueprintId}'.");
    }
}
