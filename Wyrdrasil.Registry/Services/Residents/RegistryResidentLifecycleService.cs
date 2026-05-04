using System.Collections.Generic;
using BepInEx.Logging;
using Wyrdrasil.Core.Tool;
using Wyrdrasil.Registry.Components;
using Wyrdrasil.Settlements.Services;
using Wyrdrasil.Souls.Services;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Registry.Services;

public sealed class RegistryResidentLifecycleService
{
    private readonly ManualLogSource _log;
    private readonly RegistryToolState _toolState;
    private readonly ResidentCatalogService _catalogService;
    private readonly ResidentVisualService _visualService;
    private readonly ResidentRuntimeService _runtimeService;
    private readonly ResidentPresenceService _presenceService;
    private readonly ResidentAssignmentService _assignmentService;
    private readonly RegistryResidentRegistrationService _registrationService;
    private readonly SeatService _seatService;

    public RegistryResidentLifecycleService(
        ManualLogSource log,
        RegistryToolState toolState,
        ResidentCatalogService catalogService,
        ResidentVisualService visualService,
        ResidentRuntimeService runtimeService,
        ResidentPresenceService presenceService,
        ResidentAssignmentService assignmentService,
        RegistryResidentRegistrationService registrationService,
        SeatService seatService)
    {
        _log = log;
        _toolState = toolState;
        _catalogService = catalogService;
        _visualService = visualService;
        _runtimeService = runtimeService;
        _presenceService = presenceService;
        _assignmentService = assignmentService;
        _registrationService = registrationService;
        _seatService = seatService;
    }

    public IReadOnlyList<RegisteredNpcData> RegisteredNpcs => _catalogService.RegisteredNpcs;
    public int NextRegisteredNpcId => _catalogService.NextRegisteredNpcId;
    public IReadOnlyDictionary<int, WyrdrasilRegisteredNpcMarker> Markers => _visualService.Markers;

    public void LoadResidents(IEnumerable<RegisteredNpcData> residents, int nextResidentId)
    {
        _catalogService.LoadResidents(residents, nextResidentId);

        foreach (var resident in _catalogService.RegisteredNpcs)
        {
            _registrationService.NormalizeResidentAfterLoad(resident, _seatService);
        }

        _visualService.ClearAll();
    }

    public bool TryGetResidentById(int residentId, out RegisteredNpcData resident)
    {
        return _catalogService.TryGetResidentById(residentId, out resident!);
    }

    public void PrepareResidentPresenceSnapshotsForSave()
    {
        _presenceService.PrepareResidentPresenceSnapshotsForSave();
    }

    public void RestoreResidentsAfterLoad()
    {
        _presenceService.RestoreResidentsAfterLoad();
    }

    public int ClearStaleConstructionAssignments()
    {
        return _assignmentService.ClearStaleConstructionAssignments();
    }

    public void ClearAllResidents()
    {
        foreach (var resident in _catalogService.RegisteredNpcs)
        {
            _runtimeService.TryDespawnResident(resident.Id);
            resident.PresenceSnapshot.Clear();
        }

        _catalogService.Clear();
        _visualService.ClearAll();
        _toolState.ClearPendingResidentForceAssign();
    }

    public void SetPendingForceAssignResidentVisual(int? residentId)
    {
        _visualService.SetPendingForceAssignResidentVisual(residentId);
    }

    public void SetPendingConstructionAssignmentResidentVisual(int? residentId)
    {
        _visualService.SetPendingConstructionAssignmentResidentVisual(residentId);
    }

    public void HandleDeletedSlot(int slotId)
    {
        _assignmentService.HandleDeletedSlot(slotId);
    }

    public void HandleDeletedSeat(int seatId)
    {
        _assignmentService.HandleDeletedSeat(seatId);
    }

    public void HandleDeletedBed(int bedId)
    {
        _assignmentService.HandleDeletedBed(bedId);
    }

    public void HandleDeletedCraftStation(int craftStationId)
    {
        _assignmentService.HandleDeletedCraftStation(craftStationId);
    }
}
