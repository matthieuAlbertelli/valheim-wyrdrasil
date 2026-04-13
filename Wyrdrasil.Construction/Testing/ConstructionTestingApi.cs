using System.Text;
using Wyrdrasil.Construction.Diagnostics;
using Wyrdrasil.Construction.Models;
using Wyrdrasil.Construction.Services;

namespace Wyrdrasil.Construction.Testing;

public sealed class ConstructionTestingApi : IConstructionTestingApi
{
    private readonly ConstructionProjectService _constructionProjectService;
    private readonly BlueprintCatalogService _blueprintCatalogService;
    private readonly ConstructionPlacementService _constructionPlacementService;
    private readonly ConstructionPieceBuildService _constructionPieceBuildService;
    private readonly ConstructionDebugStateService _debugStateService;
    private readonly ConstructionDebugLogService _debugLogService;

    public ConstructionTestingApi(
        ConstructionProjectService constructionProjectService,
        BlueprintCatalogService blueprintCatalogService,
        ConstructionPlacementService constructionPlacementService,
        ConstructionPieceBuildService constructionPieceBuildService,
        ConstructionDebugStateService debugStateService,
        ConstructionDebugLogService debugLogService)
    {
        _constructionProjectService = constructionProjectService;
        _blueprintCatalogService = blueprintCatalogService;
        _constructionPlacementService = constructionPlacementService;
        _constructionPieceBuildService = constructionPieceBuildService;
        _debugStateService = debugStateService;
        _debugLogService = debugLogService;
    }

    public bool TryPlaceBlueprintInstantly(PlaceBlueprintInstantRequest request, out int placedPieceCount, out string failureReason)
    {
        if (!_blueprintCatalogService.TryGetBlueprint(request.BlueprintId, out var blueprint))
        {
            placedPieceCount = 0;
            failureReason = $"Unknown blueprint '{request.BlueprintId}'.";
            return false;
        }

        _debugLogService.Verbose(
            "Testing",
            $"Instant placement requested for blueprint '{request.BlueprintId}' at {request.OriginPosition}. IgnoreMaterials={request.IgnoreMaterials}.");

        return _constructionPlacementService.TryPlaceBlueprintInstantly(
            blueprint,
            request.OriginPosition,
            request.OriginRotation,
            out placedPieceCount,
            out failureReason);
    }

    public bool TryForceCompleteNextPiece(int projectId, out int pieceId, out string failureReason)
    {
        if (!_constructionPieceBuildService.TryBuildNextPiece(projectId, out pieceId, out failureReason))
        {
            return false;
        }

        _constructionProjectService.TryForceAdvanceBuiltPieceCount(projectId, out _, out _);
        return true;
    }

    public bool TryForceCompleteProject(int projectId, out int builtPieceCount, out string failureReason)
    {
        builtPieceCount = 0;

        if (!_constructionProjectService.TryGetProject(projectId, out var project))
        {
            failureReason = $"Unknown construction project {projectId}.";
            return false;
        }

        while (project.Progress.BuiltPieceCount < project.Progress.TotalPieceCount)
        {
            if (!_constructionPieceBuildService.TryBuildNextPiece(projectId, out _, out failureReason))
            {
                return false;
            }

            _constructionProjectService.TryForceAdvanceBuiltPieceCount(projectId, out _, out _);
            builtPieceCount++;
        }

        failureReason = string.Empty;
        _debugLogService.Info("Testing", $"Force-completed construction project {projectId} with {builtPieceCount} newly built pieces.");
        return true;
    }

    public bool TryResetProject(int projectId, out string failureReason)
    {
        if (!_constructionProjectService.TryGetProject(projectId, out var project))
        {
            failureReason = $"Unknown construction project {projectId}.";
            return false;
        }

        if (project.Progress.BuiltPieceCount > 0)
        {
            failureReason = "Reset is only supported before any construction piece has been instantiated in this V1.";
            return false;
        }

        var result = _constructionProjectService.TryResetProject(projectId);
        failureReason = result ? string.Empty : $"Unknown construction project {projectId}.";
        return result;
    }

    public bool TryValidateProject(int projectId, out ConstructionValidationReport report, out string failureReason)
    {
        report = _constructionProjectService.ValidateProject(projectId);
        failureReason = report.ProjectId == 0 ? $"Unknown construction project {projectId}." : string.Empty;
        return report.ProjectId != 0;
    }

    public bool TryDumpProjectState(int projectId, out string dump, out string failureReason)
    {
        if (!_constructionProjectService.TryGetProject(projectId, out var project))
        {
            dump = string.Empty;
            failureReason = $"Unknown construction project {projectId}.";
            return false;
        }

        var builder = new StringBuilder();
        builder.AppendLine($"ProjectId: {project.Id}");
        builder.AppendLine($"BlueprintId: {project.BlueprintId}");
        builder.AppendLine($"State: {project.State}");
        builder.AppendLine($"Origin: {project.OriginPosition}");
        builder.AppendLine($"VerboseLoggingEnabled: {_debugStateService.Current.VerboseLoggingEnabled}");
        builder.AppendLine($"IgnoreMaterialRequirements: {_debugStateService.Current.IgnoreMaterialRequirements}");
        builder.AppendLine($"TotalPieceCount: {project.Progress.TotalPieceCount}");
        builder.AppendLine($"BuiltPieceCount: {project.Progress.BuiltPieceCount}");
        builder.AppendLine($"AccumulatedPieceWork: {project.Progress.AccumulatedPieceWork:0.##}");
        builder.AppendLine($"AssignedWorkerCount: {_constructionProjectService.GetAssignedWorkerCount(project)}");
        builder.AppendLine("WorkPosts:");
        foreach (var workPost in project.WorkPosts)
        {
            builder.AppendLine($"  WorkPost {workPost.Id}: Pos={workPost.WorldPosition}, Resident={(workPost.AssignedResidentId.HasValue ? workPost.AssignedResidentId.Value.ToString() : "none")}");
        }

        dump = builder.ToString();
        failureReason = string.Empty;
        return true;
    }

    public bool TryAssignResidentToProject(int residentId, int projectId, out int workPostId, out string failureReason)
    {
        if (!_constructionProjectService.TryAssignResidentToProject(residentId, projectId, out var workPost, out failureReason))
        {
            workPostId = 0;
            return false;
        }

        workPostId = workPost.Id;
        return true;
    }

    public bool TryClearResidentProjectAssignment(int residentId, out int projectId, out int workPostId, out string failureReason)
    {
        if (!_constructionProjectService.TryClearResidentAssignment(residentId, out projectId, out workPostId))
        {
            failureReason = $"Resident #{residentId} has no construction work post assignment.";
            return false;
        }

        failureReason = string.Empty;
        return true;
    }

    public bool ToggleVerboseLogging(out bool isEnabled)
    {
        _debugStateService.Current.VerboseLoggingEnabled = !_debugStateService.Current.VerboseLoggingEnabled;
        isEnabled = _debugStateService.Current.VerboseLoggingEnabled;
        _debugLogService.Info("Testing", $"Construction verbose logging {(isEnabled ? "enabled" : "disabled")}.");
        return true;
    }
}
