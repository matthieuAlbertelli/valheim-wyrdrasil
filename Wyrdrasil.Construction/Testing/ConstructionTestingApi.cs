using System.Text;
using Wyrdrasil.Construction.Models;
using Wyrdrasil.Construction.Diagnostics;
using Wyrdrasil.Construction.Services;

namespace Wyrdrasil.Construction.Testing;

public sealed class ConstructionTestingApi : IConstructionTestingApi
{
    private readonly ConstructionProjectService _constructionProjectService;
    private readonly ConstructionDebugStateService _debugStateService;
    private readonly ConstructionDebugLogService _debugLogService;

    public ConstructionTestingApi(
        ConstructionProjectService constructionProjectService,
        ConstructionDebugStateService debugStateService,
        ConstructionDebugLogService debugLogService)
    {
        _constructionProjectService = constructionProjectService;
        _debugStateService = debugStateService;
        _debugLogService = debugLogService;
    }

    public bool TryPlaceBlueprintInstantly(PlaceBlueprintInstantRequest request, out int placedPieceCount, out string failureReason)
    {
        placedPieceCount = 0;
        failureReason = "Instant blueprint placement is not implemented yet. The API surface is ready for wiring.";
        _debugLogService.Verbose("Testing", $"Instant placement requested for blueprint '{request.BlueprintId}' at {request.OriginPosition}. IgnoreMaterials={request.IgnoreMaterials}.");
        return false;
    }

    public bool TryForceCompleteNextPiece(int projectId, out int pieceId, out string failureReason)
    {
        var result = _constructionProjectService.TryForceCompleteNextPiece(projectId, out pieceId);
        failureReason = result ? string.Empty : $"Project {projectId} has no eligible construction piece to force-complete.";
        return result;
    }

    public bool TryForceCompleteProject(int projectId, out int builtPieceCount, out string failureReason)
    {
        var result = _constructionProjectService.TryForceCompleteProject(projectId, out builtPieceCount);
        failureReason = result ? string.Empty : $"Project {projectId} could not be force-completed.";
        return result;
    }

    public bool TryResetProject(int projectId, out string failureReason)
    {
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
        foreach (var piece in project.PieceProgress)
        {
            builder.AppendLine($"  Piece {piece.PieceId}: Built={piece.IsBuilt}, Reserved={piece.MaterialsReserved}, Work={piece.CurrentWork:0.##}/{piece.RequiredWork:0.##}");
        }

        dump = builder.ToString();
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
