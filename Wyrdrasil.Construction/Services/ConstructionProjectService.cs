using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Wyrdrasil.Construction.Diagnostics;
using Wyrdrasil.Construction.Models;
using Wyrdrasil.Settlements.Services;
using Wyrdrasil.Settlements.Tool;

namespace Wyrdrasil.Construction.Services;

public sealed class ConstructionProjectService
{
    private const float WorkbenchConstructionRadius = 20f;
    private const float WorkbenchConstructionRadiusSqr = WorkbenchConstructionRadius * WorkbenchConstructionRadius;

    private readonly ConstructionDebugLogService _debugLogService;
    private readonly BlueprintCatalogService _blueprintCatalogService;
    private readonly CraftStationService _craftStationService;
    private readonly Dictionary<int, ConstructionProjectData> _projectsById = new();
    private readonly Dictionary<int, int> _activeWorkPostIdsByResidentId = new();
    private int _nextProjectId = 1;
    private int _nextWorkPostId = 1;

    public ConstructionProjectService(
        ConstructionDebugLogService debugLogService,
        BlueprintCatalogService blueprintCatalogService,
        CraftStationService craftStationService)
    {
        _debugLogService = debugLogService;
        _blueprintCatalogService = blueprintCatalogService;
        _craftStationService = craftStationService;
    }

    public IReadOnlyList<ConstructionProjectData> Projects => _projectsById.Values.OrderBy(project => project.Id).ToList();
    public int NextProjectId => _nextProjectId;

    public ConstructionProjectData CreateProject(StructureBlueprintData blueprint, Vector3 originPosition, Quaternion originRotation)
    {
        var projectId = _nextProjectId++;
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
            WorkPosts = new List<ConstructionWorkPostData>()
        };

        _projectsById[project.Id] = project;
        _debugLogService.Info(
            "Project",
            $"Created construction project {project.Id} from blueprint '{blueprint.Id}' with {totalPieceCount} pieces. Workers now require real registered workbenches in range of the chantier.");
        return project;
    }

    public void LoadProjects(IEnumerable<ConstructionProjectData> projects, int nextProjectId)
    {
        _projectsById.Clear();
        _activeWorkPostIdsByResidentId.Clear();

        var maxWorkPostId = 0;
        foreach (var project in projects)
        {
            project.Progress ??= new ConstructionProjectProgressData();
            project.WorkPosts = (project.WorkPosts ?? new List<ConstructionWorkPostData>())
                .Where(candidate => candidate.CraftStationId > 0)
                .OrderBy(candidate => candidate.Id)
                .ToList();

            _projectsById[project.Id] = project;

            foreach (var workPost in project.WorkPosts)
            {
                SyncWorkPostAnchor(workPost);
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

    public IReadOnlyList<int> GetProjectIdsInZone(FunctionalZoneData zone)
    {
        return _projectsById.Values
            .Where(project => zone.ContainsPointHorizontally(project.OriginPosition))
            .OrderBy(project => project.Id)
            .Select(project => project.Id)
            .ToList();
    }

    public bool DeleteProject(int projectId)
    {
        if (!_projectsById.TryGetValue(projectId, out var project))
        {
            return false;
        }

        foreach (var residentId in project.WorkPosts
                     .Where(candidate => candidate.AssignedResidentId.HasValue)
                     .Select(candidate => candidate.AssignedResidentId!.Value)
                     .Distinct()
                     .ToList())
        {
            _activeWorkPostIdsByResidentId.Remove(residentId);
        }

        foreach (var workPost in project.WorkPosts.Where(candidate => candidate.CraftStationId > 0))
        {
            _craftStationService.ClearCraftStationAssignment(workPost.CraftStationId, out _);
        }

        _projectsById.Remove(projectId);
        _debugLogService.Info("Project", $"Deleted construction project {projectId}.");
        return true;
    }

    public bool TryGetWorkPost(int workPostId, out ConstructionWorkPostData workPost)
    {
        foreach (var project in _projectsById.Values)
        {
            var match = project.WorkPosts.FirstOrDefault(candidate => candidate.Id == workPostId);
            if (match != null)
            {
                SyncWorkPostAnchor(match);
                workPost = match;
                return true;
            }
        }

        workPost = new ConstructionWorkPostData();
        return false;
    }

    public bool TryGetAssignedWorkPost(int residentId, out ConstructionWorkPostData workPost)
    {
        foreach (var project in _projectsById.Values.OrderBy(candidate => candidate.Id))
        {
            var match = project.WorkPosts.FirstOrDefault(candidate => candidate.AssignedResidentId == residentId);
            if (match != null)
            {
                SyncWorkPostAnchor(match);
                workPost = match;
                return true;
            }
        }

        workPost = new ConstructionWorkPostData();
        return false;
    }

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

    public int GetActiveWorkerCount(int projectId)
    {
        if (!_projectsById.TryGetValue(projectId, out var project))
        {
            return 0;
        }

        var activeResidentIds = new HashSet<int>();
        foreach (var entry in _activeWorkPostIdsByResidentId)
        {
            if (!TryGetWorkPost(entry.Value, out var workPost) || workPost.ProjectId != projectId)
            {
                continue;
            }

            if (!workPost.AssignedResidentId.HasValue || workPost.AssignedResidentId.Value != entry.Key)
            {
                continue;
            }

            if (!IsWorkPostInRangeOfCurrentPiece(project, workPost))
            {
                continue;
            }

            activeResidentIds.Add(entry.Key);
        }

        return activeResidentIds.Count;
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
            if (existingProjectId == projectId && existingPost.CraftStationId > 0)
            {
                SyncWorkPostAnchor(existingPost);
                workPost = existingPost;
                failureReason = string.Empty;
                return true;
            }

            RemoveWorkPostBinding(existingPost, residentId);
        }

        if (!TryFindAvailableCraftStation(project, residentId, out var craftStation, out failureReason))
        {
            workPost = new ConstructionWorkPostData();
            return false;
        }

        var newWorkPost = new ConstructionWorkPostData
        {
            Id = AllocateWorkPostId(),
            ProjectId = projectId,
            CraftStationId = craftStation.Id,
            AssignedResidentId = residentId
        };

        if (!_craftStationService.ForceAssignCraftStation(craftStation.Id, residentId, out var previousResidentId, out var resolvedCraftStation) || resolvedCraftStation == null)
        {
            workPost = new ConstructionWorkPostData();
            failureReason = $"Failed to reserve workbench #{craftStation.Id} for construction project {projectId}.";
            return false;
        }

        if (previousResidentId.HasValue && previousResidentId.Value != residentId)
        {
            _craftStationService.ClearCraftStationAssignment(craftStation.Id, out _);
            workPost = new ConstructionWorkPostData();
            failureReason = $"Workbench #{craftStation.Id} is already used by resident #{previousResidentId.Value}.";
            return false;
        }

        SyncWorkPostAnchor(newWorkPost, resolvedCraftStation);
        project.WorkPosts.Add(newWorkPost);
        workPost = newWorkPost;
        failureReason = string.Empty;
        _debugLogService.Info(
            "Project",
            $"Assigned resident #{residentId} to construction project {projectId} using craft station #{newWorkPost.CraftStationId} (binding #{newWorkPost.Id}).");
        return true;
    }

    public bool TryRestoreResidentAssignment(int workPostId, int residentId)
    {
        if (!TryGetWorkPost(workPostId, out var workPost) || workPost.CraftStationId <= 0)
        {
            return false;
        }

        if (workPost.AssignedResidentId.HasValue && workPost.AssignedResidentId.Value != residentId)
        {
            return false;
        }

        if (!_craftStationService.ForceAssignCraftStation(workPost.CraftStationId, residentId, out var previousResidentId, out var craftStation) || craftStation == null)
        {
            return false;
        }

        if (previousResidentId.HasValue && previousResidentId.Value != residentId)
        {
            _craftStationService.ClearCraftStationAssignment(workPost.CraftStationId, out _);
            return false;
        }

        workPost.AssignedResidentId = residentId;
        _activeWorkPostIdsByResidentId.Remove(residentId);
        SyncWorkPostAnchor(workPost, craftStation);
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

        workPostId = workPost.Id;
        RemoveWorkPostBinding(workPost, residentId);
        _debugLogService.Info(
            "Project",
            $"Cleared construction assignment for resident #{residentId} from project {projectId}, binding #{workPostId}.");
        return true;
    }

    public bool TrySetResidentWorkActive(int residentId, int workPostId, bool isActive)
    {
        if (!TryGetWorkPost(workPostId, out var workPost) || workPost.AssignedResidentId != residentId)
        {
            if (!isActive)
            {
                _activeWorkPostIdsByResidentId.Remove(residentId);
            }

            return false;
        }

        if (isActive)
        {
            _activeWorkPostIdsByResidentId[residentId] = workPostId;
        }
        else
        {
            _activeWorkPostIdsByResidentId.Remove(residentId);
        }

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
        _activeWorkPostIdsByResidentId.Clear();
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

        foreach (var workPost in project.WorkPosts)
        {
            if (workPost.CraftStationId <= 0)
            {
                report.IsValid = false;
                report.Messages.Add($"Binding #{workPost.Id} has no linked craft station.");
            }
        }

        var duplicateStationIds = project.WorkPosts
            .Where(candidate => candidate.CraftStationId > 0)
            .GroupBy(candidate => candidate.CraftStationId)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        foreach (var craftStationId in duplicateStationIds)
        {
            report.IsValid = false;
            report.Messages.Add($"Craft station #{craftStationId} is bound more than once to project {projectId}.");
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

    private void RemoveWorkPostBinding(ConstructionWorkPostData workPost, int residentId)
    {
        _activeWorkPostIdsByResidentId.Remove(residentId);
        if (workPost.CraftStationId > 0)
        {
            _craftStationService.ClearCraftStationAssignment(workPost.CraftStationId, out _);
        }

        if (_projectsById.TryGetValue(workPost.ProjectId, out var project))
        {
            project.WorkPosts.Remove(workPost);
        }
    }

    private bool TryFindAvailableCraftStation(ConstructionProjectData project, int residentId, out RegisteredCraftStationData craftStation, out string failureReason)
    {
        var eligibleStations = _craftStationService.CraftStations
            .Where(candidate => candidate.HasRuntimeBinding)
            .Where(candidate => !candidate.AssignedRegisteredNpcId.HasValue || candidate.AssignedRegisteredNpcId.Value == residentId)
            .Where(candidate => !IsCraftStationBoundToAnotherProject(candidate.Id, project.Id))
            .Where(candidate => IsCraftStationEligibleForProject(candidate, project))
            .OrderBy(candidate => (candidate.ReferenceWorldPosition - project.OriginPosition).sqrMagnitude)
            .ThenBy(candidate => candidate.Id)
            .ToList();

        if (eligibleStations.Count == 0)
        {
            craftStation = null!;
            failureReason = $"Construction project {project.Id} has no eligible free workbench in range. Place and register a workbench near the chantier.";
            return false;
        }

        craftStation = eligibleStations[0];
        failureReason = string.Empty;
        return true;
    }

    private bool IsCraftStationBoundToAnotherProject(int craftStationId, int projectId)
    {
        return _projectsById.Values.Any(project =>
            project.Id != projectId &&
            project.WorkPosts.Any(workPost => workPost.CraftStationId == craftStationId));
    }

    private bool IsCraftStationEligibleForProject(RegisteredCraftStationData craftStation, ConstructionProjectData project)
    {
        if (!TryGetOrderedBlueprintPieces(project, out var orderedPieces))
        {
            return false;
        }

        var stationPosition = craftStation.ReferenceWorldPosition;
        var firstRemainingIndex = System.Math.Max(0, System.Math.Min(project.Progress.BuiltPieceCount, orderedPieces.Count));
        for (var index = firstRemainingIndex; index < orderedPieces.Count; index++)
        {
            var worldPosition = project.OriginPosition + (project.OriginRotation * orderedPieces[index].LocalPosition);
            if ((worldPosition - stationPosition).sqrMagnitude <= WorkbenchConstructionRadiusSqr)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsWorkPostInRangeOfCurrentPiece(ConstructionProjectData project, ConstructionWorkPostData workPost)
    {
        if (workPost.CraftStationId <= 0 ||
            !_craftStationService.TryGetCraftStationById(workPost.CraftStationId, out var craftStation) ||
            !craftStation.HasRuntimeBinding)
        {
            return false;
        }

        if (!TryGetNextPieceWorldPosition(project, out var nextPieceWorldPosition))
        {
            return false;
        }

        return (nextPieceWorldPosition - craftStation.ReferenceWorldPosition).sqrMagnitude <= WorkbenchConstructionRadiusSqr;
    }

    private bool TryGetNextPieceWorldPosition(ConstructionProjectData project, out Vector3 nextPieceWorldPosition)
    {
        nextPieceWorldPosition = Vector3.zero;

        if (!TryGetOrderedBlueprintPieces(project, out var orderedPieces))
        {
            return false;
        }

        var pieceIndex = project.Progress.BuiltPieceCount;
        if (pieceIndex < 0 || pieceIndex >= orderedPieces.Count)
        {
            return false;
        }

        nextPieceWorldPosition = project.OriginPosition + (project.OriginRotation * orderedPieces[pieceIndex].LocalPosition);
        return true;
    }

    private bool TryGetOrderedBlueprintPieces(ConstructionProjectData project, out List<BlueprintPieceData> orderedPieces)
    {
        orderedPieces = new List<BlueprintPieceData>();
        if (!_blueprintCatalogService.TryGetBlueprint(project.BlueprintId, out var blueprint))
        {
            return false;
        }

        orderedPieces = blueprint.Pieces
            .OrderBy(piece => piece.BuildOrder)
            .ThenBy(piece => piece.PieceId)
            .ToList();

        return orderedPieces.Count > 0;
    }

    private bool SyncWorkPostAnchor(ConstructionWorkPostData workPost)
    {
        return workPost.CraftStationId > 0 &&
               _craftStationService.TryGetCraftStationById(workPost.CraftStationId, out var craftStation) &&
               SyncWorkPostAnchor(workPost, craftStation);
    }

    private static bool SyncWorkPostAnchor(ConstructionWorkPostData workPost, RegisteredCraftStationData craftStation)
    {
        if (!craftStation.TryResolveWorldAnchor(out var anchorWorldPosition, out var anchorWorldForward))
        {
            return false;
        }

        workPost.WorldPosition = anchorWorldPosition;
        workPost.WorldRotation = Quaternion.LookRotation(anchorWorldForward, Vector3.up);
        return true;
    }
}
