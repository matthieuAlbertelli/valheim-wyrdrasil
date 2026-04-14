using BepInEx.Logging;
using Wyrdrasil.Construction.Runtime;
using Wyrdrasil.Construction.Services;
using Wyrdrasil.Construction.Testing;
using Wyrdrasil.Registry.Services;
using Wyrdrasil.Settlements.Services;

namespace Wyrdrasil.Registry.Handlers;

public sealed class DumpLatestConstructionProjectStateHandler : IRegistryActionHandler
{
    private readonly ManualLogSource _log;
    private readonly IConstructionTestingApi _constructionTestingApi;
    private readonly ConstructionDebugSessionService _constructionDebugSessionService;

    public DumpLatestConstructionProjectStateHandler(
        ManualLogSource log,
        IConstructionTestingApi constructionTestingApi,
        ConstructionDebugSessionService constructionDebugSessionService)
    {
        _log = log;
        _constructionTestingApi = constructionTestingApi;
        _constructionDebugSessionService = constructionDebugSessionService;
    }

    public void Execute()
    {
        if (!_constructionDebugSessionService.TryGetLatestProjectId(out var projectId))
        {
            _log.LogWarning("No latest construction project is available to dump.");
            return;
        }

        if (!_constructionTestingApi.TryDumpProjectState(projectId, out var dump, out var failureReason))
        {
            _log.LogWarning($"Failed to dump construction project {projectId}: {failureReason}");
            return;
        }

        _log.LogInfo($"Construction project {projectId} state dump:");
        foreach (var line in dump.Split('\n'))
        {
            _log.LogInfo(line.TrimEnd('\r'));
        }
    }
}

public sealed class AssignTargetCraftStationToConstructionProjectHandler : IRegistryActionHandler
{
    private readonly ManualLogSource _log;
    private readonly CraftStationService _craftStationService;
    private readonly IConstructionRuntimeApi _constructionRuntimeApi;
    private readonly ConstructionProjectMarkerService _constructionProjectMarkerService;
    private readonly ConstructionDebugSessionService _constructionDebugSessionService;

    public AssignTargetCraftStationToConstructionProjectHandler(
        ManualLogSource log,
        CraftStationService craftStationService,
        IConstructionRuntimeApi constructionRuntimeApi,
        ConstructionProjectMarkerService constructionProjectMarkerService,
        ConstructionDebugSessionService constructionDebugSessionService)
    {
        _log = log;
        _craftStationService = craftStationService;
        _constructionRuntimeApi = constructionRuntimeApi;
        _constructionProjectMarkerService = constructionProjectMarkerService;
        _constructionDebugSessionService = constructionDebugSessionService;
    }

    public void Execute()
    {
        if (!_constructionDebugSessionService.TryGetPendingCraftStationProjectId(out var projectId))
        {
            if (!_constructionProjectMarkerService.TryGetTargetedProjectId(out projectId))
            {
                _log.LogWarning("Aim at a construction marker to select a chantier for workbench association.");
                return;
            }

            _constructionDebugSessionService.BeginPendingCraftStationSelection(projectId);
            _log.LogInfo($"Selected construction project #{projectId} for workbench association. Now aim at a designated workbench and click again.");
            return;
        }

        if (_constructionProjectMarkerService.TryGetTargetedProjectId(out var reselectedProjectId))
        {
            _constructionDebugSessionService.BeginPendingCraftStationSelection(reselectedProjectId);
            _log.LogInfo($"Switched selected construction project to #{reselectedProjectId} for workbench association. Now aim at a designated workbench and click again.");
            return;
        }

        if (!_craftStationService.TryGetOrDesignateCraftStationAtCrosshair(out var craftStation, out var failureReason))
        {
            _log.LogWarning(failureReason);
            return;
        }

        if (craftStation.AssignedRegisteredNpcId.HasValue)
        {
            _log.LogWarning($"Workbench #{craftStation.Id} is already assigned to resident #{craftStation.AssignedRegisteredNpcId.Value} for regular work. Clear that assignment before reserving it for a chantier.");
            return;
        }

        if (!_constructionRuntimeApi.TryAssignCraftStationToProject(craftStation.Id, projectId, out var workPost, out var assignmentFailureReason))
        {
            _log.LogWarning($"Failed to associate workbench #{craftStation.Id} with construction project #{projectId}: {assignmentFailureReason}");
            return;
        }

        _constructionDebugSessionService.ClearPendingCraftStationSelection();
        _constructionDebugSessionService.SetLatestProjectId(projectId);
        _log.LogInfo($"Associated designated workbench #{craftStation.Id} with construction project #{projectId}, logical slot #{workPost.Id}.");
    }
}

public sealed class AssignTargetResidentToLatestConstructionProjectHandler : IRegistryActionHandler
{
    private readonly ManualLogSource _log;
    private readonly RegistryResidentService _residentService;
    private readonly ConstructionProjectMarkerService _constructionProjectMarkerService;
    private readonly ConstructionDebugSessionService _constructionDebugSessionService;

    public AssignTargetResidentToLatestConstructionProjectHandler(
        ManualLogSource log,
        RegistryResidentService residentService,
        ConstructionProjectMarkerService constructionProjectMarkerService,
        ConstructionDebugSessionService constructionDebugSessionService)
    {
        _log = log;
        _residentService = residentService;
        _constructionProjectMarkerService = constructionProjectMarkerService;
        _constructionDebugSessionService = constructionDebugSessionService;
    }

    public void Execute()
    {
        if (!_constructionDebugSessionService.TryGetPendingResidentProjectId(out var projectId))
        {
            if (!_constructionProjectMarkerService.TryGetTargetedProjectId(out projectId))
            {
                _log.LogWarning("Aim at a construction marker to select a chantier for resident assignment.");
                return;
            }

            _constructionDebugSessionService.BeginPendingResidentSelection(projectId);
            _log.LogInfo($"Selected construction project #{projectId} for resident assignment. Now aim at a registered resident and click again.");
            return;
        }

        if (_constructionProjectMarkerService.TryGetTargetedProjectId(out var reselectedProjectId))
        {
            _constructionDebugSessionService.BeginPendingResidentSelection(reselectedProjectId);
            _log.LogInfo($"Switched selected construction project to #{reselectedProjectId} for resident assignment. Now aim at a registered resident and click again.");
            return;
        }

        if (!_residentService.TryAssignTargetedResidentToConstructionProject(projectId, out var resident, out var workPostId, out var failureReason))
        {
            _log.LogWarning($"Failed to assign targeted resident to construction project #{projectId}: {failureReason}");
            return;
        }

        _constructionDebugSessionService.ClearPendingResidentSelection();
        _constructionDebugSessionService.SetLatestProjectId(projectId);
        _log.LogInfo($"Assigned resident #{resident.Id} ('{resident.DisplayName}') to construction project #{projectId}, logical slot #{workPostId}.");
    }
}

public sealed class ClearTargetResidentConstructionAssignmentHandler : IRegistryActionHandler
{
    private readonly ManualLogSource _log;
    private readonly RegistryResidentService _residentService;

    public ClearTargetResidentConstructionAssignmentHandler(ManualLogSource log, RegistryResidentService residentService)
    {
        _log = log;
        _residentService = residentService;
    }

    public void Execute()
    {
        if (!_residentService.TryClearTargetedResidentConstructionAssignment(out var resident, out var projectId, out var workPostId, out var failureReason))
        {
            _log.LogWarning(failureReason);
            return;
        }

        _log.LogInfo($"Cleared construction assignment for resident #{resident.Id} ('{resident.DisplayName}') from construction project {projectId}, workbench binding #{workPostId}.");
    }
}

public sealed class ForceCompleteLatestConstructionProjectHandler : IRegistryActionHandler
{
    private readonly ManualLogSource _log;
    private readonly IConstructionTestingApi _constructionTestingApi;
    private readonly ConstructionDebugSessionService _constructionDebugSessionService;

    public ForceCompleteLatestConstructionProjectHandler(
        ManualLogSource log,
        IConstructionTestingApi constructionTestingApi,
        ConstructionDebugSessionService constructionDebugSessionService)
    {
        _log = log;
        _constructionTestingApi = constructionTestingApi;
        _constructionDebugSessionService = constructionDebugSessionService;
    }

    public void Execute()
    {
        if (!_constructionDebugSessionService.TryGetLatestProjectId(out var projectId))
        {
            _log.LogWarning("No latest construction project is available to force-complete.");
            return;
        }

        if (!_constructionTestingApi.TryForceCompleteProject(projectId, out var builtPieceCount, out var failureReason))
        {
            _log.LogWarning($"Failed to force-complete construction project {projectId}: {failureReason}");
            return;
        }

        _log.LogInfo($"Force-completed construction project {projectId}. Newly completed pieces: {builtPieceCount}.");
    }
}

public sealed class ResetLatestConstructionProjectHandler : IRegistryActionHandler
{
    private readonly ManualLogSource _log;
    private readonly IConstructionTestingApi _constructionTestingApi;
    private readonly ConstructionDebugSessionService _constructionDebugSessionService;

    public ResetLatestConstructionProjectHandler(
        ManualLogSource log,
        IConstructionTestingApi constructionTestingApi,
        ConstructionDebugSessionService constructionDebugSessionService)
    {
        _log = log;
        _constructionTestingApi = constructionTestingApi;
        _constructionDebugSessionService = constructionDebugSessionService;
    }

    public void Execute()
    {
        if (!_constructionDebugSessionService.TryGetLatestProjectId(out var projectId))
        {
            _log.LogWarning("No latest construction project is available to reset.");
            return;
        }

        if (!_constructionTestingApi.TryResetProject(projectId, out var failureReason))
        {
            _log.LogWarning($"Failed to reset construction project {projectId}: {failureReason}");
            return;
        }

        _log.LogInfo($"Reset construction project {projectId}.");
    }
}

public sealed class ToggleConstructionVerboseLoggingHandler : IRegistryActionHandler
{
    private readonly ManualLogSource _log;
    private readonly IConstructionTestingApi _constructionTestingApi;

    public ToggleConstructionVerboseLoggingHandler(ManualLogSource log, IConstructionTestingApi constructionTestingApi)
    {
        _log = log;
        _constructionTestingApi = constructionTestingApi;
    }

    public void Execute()
    {
        _constructionTestingApi.ToggleVerboseLogging(out var isEnabled);
        _log.LogInfo($"Construction verbose logging {(isEnabled ? "enabled" : "disabled")}.");
    }
}
