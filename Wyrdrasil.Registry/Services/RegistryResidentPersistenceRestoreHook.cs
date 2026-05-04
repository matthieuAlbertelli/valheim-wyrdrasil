using System.Linq;
using Wyrdrasil.Construction.Runtime;
using Wyrdrasil.Core.Persistence;
using Wyrdrasil.Core.Tool;
using Wyrdrasil.Settlements.Runtime;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Registry.Services;

public sealed class RegistryResidentPersistenceRestoreHook : IWorldPersistenceRestoreHook
{
    private readonly ISettlementsRuntimeApi _settlementsRuntimeApi;
    private readonly IConstructionRuntimeApi _constructionRuntimeApi;
    private readonly RegistryResidentService _residentService;
    private readonly ResidentRoutineService _residentRoutineService;

    public RegistryResidentPersistenceRestoreHook(
        ISettlementsRuntimeApi settlementsRuntimeApi,
        IConstructionRuntimeApi constructionRuntimeApi,
        RegistryResidentService residentService,
        ResidentRoutineService residentRoutineService)
    {
        _settlementsRuntimeApi = settlementsRuntimeApi;
        _constructionRuntimeApi = constructionRuntimeApi;
        _residentService = residentService;
        _residentRoutineService = residentRoutineService;
    }

    public void OnAfterRestore()
    {
        _residentService.NormalizeResidentsAfterLoad();
        RebuildAssignmentsFromResidents();
        _residentService.RestoreResidentsAfterLoad();
        _residentRoutineService.ForceRefreshAllResidents(true);
        _residentRoutineService.ScheduleForcedRefresh(1f, true);
    }

    public void OnAfterDeferredResolutions()
    {
        RebuildAssignmentsFromResidents();
        _residentService.RestoreResidentsAfterLoad();
        _residentRoutineService.ForceRefreshAllResidents(true);
        _residentRoutineService.ScheduleForcedRefresh(1f, true);
    }

    private void RebuildAssignmentsFromResidents()
    {
        foreach (var resident in _residentService.RegisteredNpcs)
        {
            foreach (var assignment in resident.Assignments.ToArray())
            {
                if (TryRestoreAssignment(resident, assignment))
                {
                    continue;
                }

                resident.ClearAssignment(assignment.Purpose);
                if (assignment.Purpose == ResidentAssignmentPurpose.Work && resident.Role == NpcRole.Innkeeper)
                {
                    resident.SetRole(NpcRole.Villager);
                }
            }
        }
    }

    private bool TryRestoreAssignment(RegisteredNpcData resident, ResidentAssignmentData assignment)
    {
        return assignment.Target.TargetKind == OccupationTargetKind.ConstructionWorkPost
            ? _constructionRuntimeApi.TryRestoreResidentAssignment(assignment.Target.TargetId, resident.Id)
            : _settlementsRuntimeApi.TryRestoreResidentAssignment(assignment.Target.TargetKind, assignment.Target.TargetId, resident.Id);
    }
}
