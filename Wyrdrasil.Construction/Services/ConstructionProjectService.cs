using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Wyrdrasil.Construction.Diagnostics;
using Wyrdrasil.Construction.Models;

namespace Wyrdrasil.Construction.Services;

public sealed class ConstructionProjectService
{
    private readonly ConstructionDebugLogService _debugLogService;
    private readonly Dictionary<int, ConstructionProjectData> _projectsById = new();
    private int _nextProjectId = 1;

    public ConstructionProjectService(ConstructionDebugLogService debugLogService)
    {
        _debugLogService = debugLogService;
    }

    public IReadOnlyList<ConstructionProjectData> Projects => _projectsById.Values.OrderBy(project => project.Id).ToList();
    public int NextProjectId => _nextProjectId;

    public ConstructionProjectData CreateProject(StructureBlueprintData blueprint, Vector3 originPosition, Quaternion originRotation)
    {
        var project = new ConstructionProjectData
        {
            Id = _nextProjectId++,
            BlueprintId = blueprint.Id,
            OriginPosition = originPosition,
            OriginRotation = originRotation,
            State = blueprint.Pieces.Count == 0 ? ConstructionProjectState.Blocked : ConstructionProjectState.ReadyForWork,
            PieceProgress = blueprint.Pieces
                .OrderBy(piece => piece.BuildOrder)
                .Select(piece => new ConstructionPieceProgressData
                {
                    PieceId = piece.PieceId,
                    RequiredWork = 1f
                })
                .ToList()
        };

        _projectsById[project.Id] = project;
        _debugLogService.Info("Project", $"Created project {project.Id} from blueprint '{blueprint.Id}' with {project.PieceProgress.Count} pieces.");
        return project;
    }

    public void LoadProjects(IEnumerable<ConstructionProjectData> projects, int nextProjectId)
    {
        _projectsById.Clear();
        foreach (var project in projects)
        {
            _projectsById[project.Id] = project;
        }

        _nextProjectId = nextProjectId;
    }

    public bool TryGetProject(int projectId, out ConstructionProjectData project) => _projectsById.TryGetValue(projectId, out project!);
    public bool IsProjectActive(int projectId) => _projectsById.TryGetValue(projectId, out var project) && project.State != ConstructionProjectState.Completed;

    public bool TryAdvancePieceWork(int projectId, int pieceId, float workAmount)
    {
        if (!_projectsById.TryGetValue(projectId, out var project))
        {
            return false;
        }

        var piece = project.PieceProgress.FirstOrDefault(candidate => candidate.PieceId == pieceId);
        if (piece == null || piece.IsBuilt)
        {
            return false;
        }

        piece.CurrentWork += Mathf.Max(0f, workAmount);
        project.State = ConstructionProjectState.InProgress;
        if (piece.CurrentWork >= piece.RequiredWork)
        {
            piece.CurrentWork = piece.RequiredWork;
            piece.IsBuilt = true;
        }

        if (project.PieceProgress.All(candidate => candidate.IsBuilt))
        {
            project.State = ConstructionProjectState.Completed;
        }

        return true;
    }

    public bool TryForceCompleteNextPiece(int projectId, out int pieceId)
    {
        pieceId = 0;
        if (!_projectsById.TryGetValue(projectId, out var project))
        {
            return false;
        }

        var piece = project.PieceProgress.OrderBy(candidate => candidate.PieceId).FirstOrDefault(candidate => !candidate.IsBuilt);
        if (piece == null)
        {
            return false;
        }

        piece.CurrentWork = piece.RequiredWork;
        piece.IsBuilt = true;
        pieceId = piece.PieceId;
        if (project.PieceProgress.All(candidate => candidate.IsBuilt))
        {
            project.State = ConstructionProjectState.Completed;
        }

        _debugLogService.Info("Testing", $"Force-completed piece {pieceId} on project {projectId}.");
        return true;
    }

    public bool TryForceCompleteProject(int projectId, out int builtPieceCount)
    {
        builtPieceCount = 0;
        if (!_projectsById.TryGetValue(projectId, out var project))
        {
            return false;
        }

        foreach (var piece in project.PieceProgress.Where(candidate => !candidate.IsBuilt))
        {
            piece.CurrentWork = piece.RequiredWork;
            piece.IsBuilt = true;
            builtPieceCount++;
        }

        project.State = ConstructionProjectState.Completed;
        _debugLogService.Info("Testing", $"Force-completed project {projectId} with {builtPieceCount} newly built pieces.");
        return true;
    }

    public bool TryResetProject(int projectId)
    {
        if (!_projectsById.TryGetValue(projectId, out var project))
        {
            return false;
        }

        foreach (var piece in project.PieceProgress)
        {
            piece.IsBuilt = false;
            piece.MaterialsReserved = false;
            piece.CurrentWork = 0f;
        }

        project.State = project.PieceProgress.Count == 0 ? ConstructionProjectState.Blocked : ConstructionProjectState.ReadyForWork;
        _debugLogService.Info("Testing", $"Reset project {projectId} to state {project.State}.");
        return true;
    }

    public ConstructionValidationReport ValidateProject(int projectId)
    {
        if (!_projectsById.TryGetValue(projectId, out var project))
        {
            return new ConstructionValidationReport
            {
                ProjectId = 0,
                IsValid = false,
                Messages = { $"Unknown project {projectId}." }
            };
        }

        var report = new ConstructionValidationReport
        {
            ProjectId = projectId,
            IsValid = true
        };

        if (project.PieceProgress.Count == 0)
        {
            report.IsValid = false;
            report.Messages.Add("Project contains no construction pieces.");
        }

        if (string.IsNullOrWhiteSpace(project.BlueprintId))
        {
            report.IsValid = false;
            report.Messages.Add("Project is missing a blueprint id.");
        }

        if (report.Messages.Count == 0)
        {
            report.Messages.Add("Validation passed. Detailed placement checks are not implemented yet.");
        }

        return report;
    }
}
