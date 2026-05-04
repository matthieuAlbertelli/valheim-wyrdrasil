using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;
using Wyrdrasil.Construction.Authoring;
using Wyrdrasil.Construction.Models;
using Wyrdrasil.Construction.Services;
using Wyrdrasil.Registry.Services;
using Wyrdrasil.Settlements.Authoring;

namespace Wyrdrasil.Registry.Handlers;

public sealed class CaptureBlueprintFromTargetZoneHandler : IRegistryActionHandler
{
    private readonly ManualLogSource _log;
    private readonly ISettlementsAuthoringApi _settlementsAuthoringApi;
    private readonly IConstructionAuthoringApi _constructionAuthoringApi;
    private readonly ConstructionDebugSessionService _constructionDebugSessionService;

    public CaptureBlueprintFromTargetZoneHandler(
        ManualLogSource log,
        ISettlementsAuthoringApi settlementsAuthoringApi,
        IConstructionAuthoringApi constructionAuthoringApi,
        ConstructionDebugSessionService constructionDebugSessionService)
    {
        _log = log;
        _settlementsAuthoringApi = settlementsAuthoringApi;
        _constructionAuthoringApi = constructionAuthoringApi;
        _constructionDebugSessionService = constructionDebugSessionService;
    }

    public void Execute()
    {
        if (!_settlementsAuthoringApi.TryGetPlacementPoint(out var point) || !_settlementsAuthoringApi.TryFindZoneAtPoint(point, out var zone))
        {
            _log.LogWarning("Cannot capture construction blueprint: no functional zone was found under the crosshair.");
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

        if (!_constructionAuthoringApi.TryCaptureBlueprintFromZone(request, out var blueprint, out var failureReason))
        {
            _log.LogWarning($"Failed to capture construction blueprint from zone #{zone.Id}: {failureReason}");
            return;
        }

        _constructionDebugSessionService.SetLatestBlueprintId(blueprint.Id);
        _log.LogInfo($"Captured blueprint '{blueprint.DisplayName}' ({blueprint.Id}) from zone #{zone.Id} with {blueprint.Pieces.Count} pieces. Use the Construction placement action to place it elsewhere.");
    }
}

public sealed class SpawnTestConstructionProjectHandler : IRegistryActionHandler
{
    private const string BlueprintId = "debug.tavern.frame.small";

    private readonly ManualLogSource _log;
    private readonly IConstructionAuthoringApi _constructionAuthoringApi;
    private readonly ConstructionDebugSessionService _constructionDebugSessionService;

    public SpawnTestConstructionProjectHandler(
        ManualLogSource log,
        IConstructionAuthoringApi constructionAuthoringApi,
        ConstructionDebugSessionService constructionDebugSessionService)
    {
        _log = log;
        _constructionAuthoringApi = constructionAuthoringApi;
        _constructionDebugSessionService = constructionDebugSessionService;
    }

    public void Execute()
    {
        var player = Player.m_localPlayer;
        if (player == null)
        {
            _log.LogWarning("Cannot spawn a test construction project because there is no local player.");
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

        if (!_constructionAuthoringApi.TryRegisterBlueprint(blueprint, out var registerFailure))
        {
            _log.LogWarning($"Failed to register test construction blueprint: {registerFailure}");
            return;
        }

        var origin = player.transform.position + (player.transform.forward * 4f);
        var request = new CreateConstructionProjectRequest
        {
            BlueprintId = BlueprintId,
            OriginPosition = origin,
            OriginRotation = Quaternion.identity
        };

        if (!_constructionAuthoringApi.TryCreateProject(request, out var project, out var createFailure))
        {
            _log.LogWarning($"Failed to create test construction project: {createFailure}");
            return;
        }

        _constructionDebugSessionService.SetLatestBlueprintAndProject(BlueprintId, project.Id);
        _log.LogInfo($"Spawned test construction project {project.Id} from blueprint '{BlueprintId}' at {origin}.");
    }
}

public sealed class PlaceBlueprintInstantlyHandler : IRegistryActionHandler
{
    private readonly ManualLogSource _log;
    private readonly ISettlementsAuthoringApi _settlementsAuthoringApi;
    private readonly ConstructionPlacementPreviewService _constructionPlacementPreviewService;
    private readonly ConstructionDebugSessionService _constructionDebugSessionService;

    public PlaceBlueprintInstantlyHandler(
        ManualLogSource log,
        ISettlementsAuthoringApi settlementsAuthoringApi,
        ConstructionPlacementPreviewService constructionPlacementPreviewService,
        ConstructionDebugSessionService constructionDebugSessionService)
    {
        _log = log;
        _settlementsAuthoringApi = settlementsAuthoringApi;
        _constructionPlacementPreviewService = constructionPlacementPreviewService;
        _constructionDebugSessionService = constructionDebugSessionService;
    }

    public void Execute()
    {
        var player = Player.m_localPlayer;
        if (player == null)
        {
            _log.LogWarning("No local player for construction placement preview.");
            return;
        }

        if (!_constructionDebugSessionService.TryGetLatestBlueprintId(out var blueprintId))
        {
            _log.LogWarning("No latest construction blueprint is available for preview. Capture a zone blueprint or spawn a test construction project first.");
            return;
        }

        Vector3 originPosition;
        if (_settlementsAuthoringApi.TryGetPlacementPoint(out var placementPoint))
        {
            originPosition = placementPoint;
        }
        else
        {
            originPosition = player.transform.position + (player.transform.forward * 4f);
        }

        if (!_constructionPlacementPreviewService.TryBeginPreview(
                blueprintId,
                originPosition,
                Quaternion.identity,
                out var failureReason))
        {
            _log.LogWarning($"Failed to start construction placement preview: {failureReason}");
            return;
        }

        _log.LogInfo($"Started construction placement preview for blueprint '{blueprintId}'.");
    }
}
