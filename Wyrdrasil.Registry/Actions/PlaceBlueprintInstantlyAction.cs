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

        var request = new PlaceBlueprintInstantRequest
        {
            BlueprintId = "debug.tavern.frame.small",
            OriginPosition = player.transform.position + player.transform.forward * 4f,
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
