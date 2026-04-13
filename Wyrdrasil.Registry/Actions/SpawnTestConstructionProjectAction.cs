using System.Collections.Generic;
using UnityEngine;
using Wyrdrasil.Construction.Authoring;
using Wyrdrasil.Construction.Models;
using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Actions;

public sealed class SpawnTestConstructionProjectAction : IRegistryAction
{
    private const string BlueprintId = "debug.tavern.frame.small";

    public RegistryActionType ActionType => RegistryActionType.SpawnTestConstructionProject;

    public void Execute(RegistryContext context)
    {
        var player = Player.m_localPlayer;
        if (player == null)
        {
            context.Log.LogWarning("Cannot spawn a test construction project because there is no local player.");
            return;
        }

        var blueprint = new StructureBlueprintData
        {
            Id = BlueprintId,
            DisplayName = "Debug Tavern Frame",
            Pieces = new List<BlueprintPieceData>
            {
                new() { PieceId = 1, PrefabName = "wood_floor_1x1", LocalPosition = new Vector3(0f, 0f, 0f) },
                new() { PieceId = 2, PrefabName = "wood_wall", LocalPosition = new Vector3(0f, 0f, 2f) },
                new() { PieceId = 3, PrefabName = "wood_wall", LocalPosition = new Vector3(2f, 0f, 0f) },
                new() { PieceId = 4, PrefabName = "wood_roof", LocalPosition = new Vector3(0f, 2f, 0f) }
            }
        };

        if (!context.ConstructionAuthoringApi.TryRegisterBlueprint(blueprint, out var registerFailure))
        {
            context.Log.LogWarning($"Failed to register test construction blueprint: {registerFailure}");
            return;
        }

        var origin = player.transform.position + (player.transform.forward * 4f);
        var request = new CreateConstructionProjectRequest
        {
            BlueprintId = BlueprintId,
            OriginPosition = origin,
            OriginRotation = Quaternion.identity
        };

        if (!context.ConstructionAuthoringApi.TryCreateProject(request, out var project, out var createFailure))
        {
            context.Log.LogWarning($"Failed to create test construction project: {createFailure}");
            return;
        }

        context.ConstructionDebugSessionService.SetLatestProjectId(project.Id);
        context.Log.LogInfo($"Spawned test construction project {project.Id} from blueprint '{BlueprintId}' at {origin}.");
    }
}
