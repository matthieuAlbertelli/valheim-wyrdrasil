using System;
using System.Collections.Generic;
using System.Linq;
using Wyrdrasil.Construction.Models;
using Wyrdrasil.Construction.Diagnostics;
using Wyrdrasil.Construction.Services;

namespace Wyrdrasil.Construction.Authoring;

public sealed class ConstructionAuthoringApi : IConstructionAuthoringApi
{
    private readonly BlueprintCatalogService _blueprintCatalogService;
    private readonly ConstructionProjectService _constructionProjectService;
    private readonly ConstructionOrderService _constructionOrderService;
    private readonly ConstructionDebugLogService _debugLogService;

    public ConstructionAuthoringApi(
        BlueprintCatalogService blueprintCatalogService,
        ConstructionProjectService constructionProjectService,
        ConstructionOrderService constructionOrderService,
        ConstructionDebugLogService debugLogService)
    {
        _blueprintCatalogService = blueprintCatalogService;
        _constructionProjectService = constructionProjectService;
        _constructionOrderService = constructionOrderService;
        _debugLogService = debugLogService;
    }

    public bool TryCaptureBlueprint(ConstructionCaptureRequest request, out StructureBlueprintData blueprint, out string failureReason)
    {
        blueprint = new StructureBlueprintData
        {
            Id = Guid.NewGuid().ToString("N"),
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? "Captured Blueprint" : request.DisplayName,
            Pieces = new List<BlueprintPieceData>()
        };

        failureReason = "Blueprint capture is not implemented yet. The module scaffold is ready for integration.";
        _debugLogService.Verbose("Capture", $"Capture requested for '{blueprint.DisplayName}' at {request.Center} with size {request.Size}.");
        return false;
    }

    public bool TryRegisterBlueprint(StructureBlueprintData blueprint, out string failureReason)
    {
        if (string.IsNullOrWhiteSpace(blueprint.Id))
        {
            failureReason = "Blueprint id is required.";
            return false;
        }

        var orderedPieces = _constructionOrderService.AssignBuildOrder(blueprint.Pieces).ToList();
        blueprint.Pieces = orderedPieces;
        _blueprintCatalogService.UpsertBlueprint(blueprint);
        failureReason = string.Empty;
        _debugLogService.Info("Blueprint", $"Registered blueprint '{blueprint.DisplayName}' ({blueprint.Id}) with {blueprint.Pieces.Count} pieces.");
        return true;
    }

    public bool TryCreateProject(CreateConstructionProjectRequest request, out ConstructionProjectData project, out string failureReason)
    {
        if (!_blueprintCatalogService.TryGetBlueprint(request.BlueprintId, out var blueprint))
        {
            project = new ConstructionProjectData();
            failureReason = $"Unknown blueprint '{request.BlueprintId}'.";
            return false;
        }

        project = _constructionProjectService.CreateProject(blueprint, request.OriginPosition, request.OriginRotation);
        failureReason = string.Empty;
        return true;
    }

    public IReadOnlyList<StructureBlueprintData> GetBlueprints() => _blueprintCatalogService.Blueprints;
}
