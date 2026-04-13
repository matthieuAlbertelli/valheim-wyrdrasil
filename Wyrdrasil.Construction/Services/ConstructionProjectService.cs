using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Wyrdrasil.Construction.Diagnostics;
using Wyrdrasil.Construction.Models;

namespace Wyrdrasil.Construction.Services;

public sealed class ConstructionProjectService
{
    private readonly ConstructionDebugLogService _debugLogService;
    private readonly ConstructionWorkPostGenerationService _constructionWorkPostGenerationService;
    private readonly Dictionary<int, ConstructionProjectData> _projectsById = new();
    private int _nextProjectId = 1;
    private int _nextWorkPostId = 1;

    public ConstructionProjectService(
        ConstructionDebugLogService debugLogService,
        ConstructionWorkPostGenerationService constructionWorkPostGenerationService)
    {
        _debugLogService = debugLogService;
        _constructionWorkPostGenerationService = constructionWorkPostGenerationService;
    }

    public IReadOnlyList<ConstructionProjectData> Projects => _projectsById.Values.OrderBy(project => project.Id).ToList();
    public int NextProjectId => _nextProjectId;

    public ConstructionProjectData CreateProject(StructureBlueprintData blueprint, Vector3 originPosition, Quaternion originRotation)
    {
        var projectId = _nextProjectId++;
        var workPosts = _constructionWorkPostGenerationService.GenerateWorkPosts(
            projectId,
            blueprint,
            originPosition,
            originRotation,
            AllocateWorkPostId);

        var totalPieceCount = blueprint.Pieces.Count;
        var project = new ConstructionProjectData
        {
            Id = projectId,
            BlueprintId = blueprint.Id,
            OriginPosition = originPosition,
            OriginRotation = originRotation,
            State = totalPieceCount == 0 ? ConstructionProjectState.Blocked : ConstructionProjectState.ReadyForWork,
            Progress = new ConstructionProjectProgressData
            {
                TotalPieceCount = totalPieceCount,
                BuiltPieceCount = 0,
                AccumulatedPieceWork = 0f
            },
            WorkPosts = workPosts
        };

        _projectsById[project.Id] = project;
        _debugLogService.Info(
            "Project",
            $"Created construction project {project.Id} from blueprint '{blueprint.Id}' with {totalPieceCount} pieces and {workPosts.Count} external work posts.");
        return project;
    }

    public void LoadProjects(IEnumerable<ConstructionProjectData> projects, int nextProjectId)
    {
        _projectsById.Clear();

        var maxWorkPostId = 0;
        foreach (var project in projects)
        {
            project.Progress ??= new ConstructionProjectProgressData();
            project.WorkPosts ??= new List<ConstructionWorkPostData>();
            _projectsById[project.Id] = project;

            foreach (var workPost in project.WorkPosts)
            {
                if (workPost.Id > maxWorkPostId)
                {
                    maxWorkPostId = workPost.Id;
                }
            }
        }

        _nextProjectId = nextProjectId > 0 ? nextProjectId : 1;
        _nextWorkPostId = maxWorkPostId + 1;
    }

    public bool TryGetProject(int projectId, out ConstructionProjectData project) => _projectsById.TryGetValue(projectId, out project!);

    public bool IsProjectActive(int projectId)
    {
        return _projectsById.TryGetValue(projectId, out var project) &&
               project.State != ConstructionProjectState.Completed &&
               project.State != ConstructionProjectState.Blocked;
    }

    public int GetAssignedWorkerCount(ConstructionProjectData project)
    {
        return project.WorkPosts.Count(workPost => workPost.AssignedResidentId.HasValue);
    }

    public bool TryAssignResidentToProject(int residentId, int projectId, out ConstructionWorkPostData workPost, out string failureReason)
    {
        if (!_projectsById.TryGetValue(projectId, out var project))
        {
            workPost = new ConstructionWorkPostData();
            failureReason = $"Unknown construction project {projectId}.";
            return false;
        }

        if (project.State == ConstructionProjectState.Completed || project.State == ConstructionProjectState.Blocked)
        {
            workPost = new ConstructionWorkPostData();
            failureReason = $"Construction project {projectId} is not accepting workers because it is in state {project.State}.";
            return false;
        }

        var existingPost = FindAssignedWorkPost(residentId, out var existingProjectId);
        if (existingPost != null)
        {
            if (existingProjectId == projectId)
            {
                workPost = existingPost;
                failureReason = string.Empty;
                return true;
            }
        }

        var freePost = project.WorkPosts.FirstOrDefault(candidate => !candidate.AssignedResidentId.HasValue);
        if (freePost == null)
        {
            workPost = new ConstructionWorkPostData();
            failureReason = $"Construction project {projectId} has no free work post.";
            return false;
        }

        if (existingPost != null)
        {
            existingPost.AssignedResidentId = null;
        }

        freePost.AssignedResidentId = residentId;
        workPost = freePost;
        failureReason = string.Empty;
        _debugLogService.Info(
            "Project",
            $"Assigned resident #{residentId} to construction project {projectId}, work post #{freePost.Id}.");
        return true;
    }

    public bool TryClearResidentAssignment(int residentId, out int projectId, out int workPostId)
    {
        var workPost = FindAssignedWorkPost(residentId, out projectId);
        if (workPost == null)
        {
            workPostId = 0;
            projectId = 0;
            return false;
        }

        workPost.AssignedResidentId = null;
        workPostId = workPost.Id;
        _debugLogService.Info(
            "Project",
            $"Cleared construction assignment for resident #{residentId} from project {projectId}, work post #{workPostId}.");
        return true;
    }

    public bool TryAddAccumulatedPieceWork(int projectId, float workAmount, out float accumulatedPieceWork)
    {
        accumulatedPieceWork = 0f;
        if (!_projectsById.TryGetValue(projectId, out var project) || workAmount <= 0f)
        {
            return false;
        }

        if (project.State == ConstructionProjectState.Completed || project.State == ConstructionProjectState.Blocked)
        {
            accumulatedPieceWork = project.Progress.AccumulatedPieceWork;
            return false;
        }

        project.Progress.AccumulatedPieceWork += workAmount;
        if (project.Progress.BuiltPieceCount > 0 || project.Progress.AccumulatedPieceWork > 0f)
        {
            project.State = ConstructionProjectState.InProgress;
        }

        accumulatedPieceWork = project.Progress.AccumulatedPieceWork;
        return true;
    }

    public bool TryConsumeOnePieceWork(int projectId, out float remainingAccumulatedWork)
    {
        remainingAccumulatedWork = 0f;
        if (!_projectsById.TryGetValue(projectId, out var project))
        {
            return false;
        }

        if (project.Progress.AccumulatedPieceWork < 1f)
        {
            remainingAccumulatedWork = project.Progress.AccumulatedPieceWork;
            return false;
        }

        project.Progress.AccumulatedPieceWork -= 1f;
        remainingAccumulatedWork = project.Progress.AccumulatedPieceWork;
        return true;
    }

    public bool TryMarkNextPieceBuilt(int projectId, out int builtPieceCount, out bool isCompleted)
    {
        builtPieceCount = 0;
        isCompleted = false;

        if (!_projectsById.TryGetValue(projectId, out var project))
        {
            return false;
        }

        if (project.Progress.BuiltPieceCount >= project.Progress.TotalPieceCount)
        {
            builtPieceCount = project.Progress.BuiltPieceCount;
            isCompleted = project.State == ConstructionProjectState.Completed;
            return false;
        }

        project.Progress.BuiltPieceCount++;
        builtPieceCount = project.Progress.BuiltPieceCount;
        isCompleted = builtPieceCount >= project.Progress.TotalPieceCount;
        project.State = isCompleted ? ConstructionProjectState.Completed : ConstructionProjectState.InProgress;
        return true;
    }

    public bool TrySetState(int projectId, ConstructionProjectState state)
    {
        if (!_projectsById.TryGetValue(projectId, out var project))
        {
            return false;
        }

        project.State = state;
        return true;
    }

    public bool TryForceAdvanceBuiltPieceCount(int projectId, out int builtPieceCount, out bool isCompleted)
    {
        return TryMarkNextPieceBuilt(projectId, out builtPieceCount, out isCompleted);
    }

    public bool TryResetProject(int projectId)
    {
        if (!_projectsById.TryGetValue(projectId, out var project))
        {
            return false;
        }

        project.Progress.BuiltPieceCount = 0;
        project.Progress.AccumulatedPieceWork = 0f;
        project.State = project.Progress.TotalPieceCount == 0 ? ConstructionProjectState.Blocked : ConstructionProjectState.ReadyForWork;
        _debugLogService.Info("Testing", $"Reset construction project {projectId} to state {project.State}.");
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
                Messages = { $"Unknown construction project {projectId}." }
            };
        }

        var report = new ConstructionValidationReport
        {
            ProjectId = projectId,
            IsValid = true
        };

        if (project.Progress.TotalPieceCount <= 0)
        {
            report.IsValid = false;
            report.Messages.Add("Project contains no construction pieces.");
        }

        if (string.IsNullOrWhiteSpace(project.BlueprintId))
        {
            report.IsValid = false;
            report.Messages.Add("Project is missing a blueprint id.");
        }

        if (project.WorkPosts.Count != 4)
        {
            report.IsValid = false;
            report.Messages.Add($"Project should expose 4 external work posts but currently exposes {project.WorkPosts.Count}.");
        }

        if (project.Progress.BuiltPieceCount > project.Progress.TotalPieceCount)
        {
            report.IsValid = false;
            report.Messages.Add("BuiltPieceCount exceeds TotalPieceCount.");
        }

        if (report.Messages.Count == 0)
        {
            report.Messages.Add("Validation passed.");
        }

        return report;
    }

    private int AllocateWorkPostId() => _nextWorkPostId++;

    private ConstructionWorkPostData? FindAssignedWorkPost(int residentId, out int projectId)
    {
        foreach (var project in _projectsById.Values.OrderBy(candidate => candidate.Id))
        {
            var workPost = project.WorkPosts.FirstOrDefault(candidate => candidate.AssignedResidentId == residentId);
            if (workPost != null)
            {
                projectId = project.Id;
                return workPost;
            }
        }

        projectId = 0;
        return null;
    }
}
