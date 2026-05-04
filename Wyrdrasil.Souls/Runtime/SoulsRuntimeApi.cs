using System.Collections.Generic;
using UnityEngine;
using Wyrdrasil.Souls.Services;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Souls.Runtime;

public sealed class SoulsRuntimeApi : ISoulsRuntimeApi
{
    private readonly ResidentCatalogService _catalogService;
    private readonly ResidentRuntimeService _runtimeService;
    private readonly NpcSpawnService _spawnService;

    public SoulsRuntimeApi(
        ResidentCatalogService catalogService,
        ResidentRuntimeService runtimeService,
        NpcSpawnService spawnService)
    {
        _catalogService = catalogService;
        _runtimeService = runtimeService;
        _spawnService = spawnService;
    }

    public IReadOnlyList<RegisteredNpcData> RegisteredNpcs => _catalogService.RegisteredNpcs;
    public int NextRegisteredNpcId => _catalogService.NextRegisteredNpcId;

    public void LoadResidents(IEnumerable<RegisteredNpcData> residents, int nextResidentId)
    {
        _catalogService.LoadResidents(residents, nextResidentId);
    }

    public bool TryGetResidentById(int residentId, out RegisteredNpcData resident)
    {
        return _catalogService.TryGetResidentById(residentId, out resident!);
    }

    public void AddResident(RegisteredNpcData resident)
    {
        _catalogService.AddResident(resident);
    }

    public int AllocateResidentId()
    {
        return _catalogService.AllocateResidentId();
    }

    public void ClearResidents()
    {
        _catalogService.Clear();
    }

    public ResidentRuntimeState GetRuntimeState(int residentId)
    {
        return _runtimeService.GetRuntimeState(residentId);
    }

    public void MarkResidentSpawning(int residentId)
    {
        _runtimeService.MarkResidentSpawning(residentId);
    }

    public void MarkResidentMissing(int residentId)
    {
        _runtimeService.MarkResidentMissing(residentId);
    }

    public void BindResident(int residentId, Character character)
    {
        _runtimeService.BindResident(residentId, character);
    }

    public void UnbindResident(int residentId)
    {
        _runtimeService.UnbindResident(residentId);
    }

    public bool TryDespawnResident(int residentId)
    {
        return _runtimeService.TryDespawnResident(residentId);
    }

    public bool TryGetResidentId(Character character, out int residentId)
    {
        return _runtimeService.TryGetResidentId(character, out residentId);
    }

    public bool TryGetBoundCharacter(int residentId, out Character character)
    {
        return _runtimeService.TryGetBoundCharacter(residentId, out character!);
    }

    public bool TryCaptureBoundResidentTransform(int residentId, out Vector3 worldPosition, out float worldYawDegrees, out bool isAttached)
    {
        return _runtimeService.TryCaptureBoundResidentTransform(residentId, out worldPosition, out worldYawDegrees, out isAttached);
    }

    public bool TrySpawnResident(RegisteredNpcData resident, Vector3 spawnPosition, Quaternion spawnRotation, out Character character)
    {
        character = null!;

        if (!_spawnService.TrySpawnResident(resident, spawnPosition, spawnRotation, out var instance) || instance == null)
        {
            return false;
        }

        var resolvedCharacter = instance.GetComponent<Character>();
        if (resolvedCharacter == null)
        {
            Object.Destroy(instance);
            return false;
        }

        character = resolvedCharacter;
        return true;
    }
}
