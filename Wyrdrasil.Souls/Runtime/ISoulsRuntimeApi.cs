using System.Collections.Generic;
using UnityEngine;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Souls.Runtime;

public interface ISoulsRuntimeApi
{
    IReadOnlyList<RegisteredNpcData> RegisteredNpcs { get; }
    int NextRegisteredNpcId { get; }

    void LoadResidents(IEnumerable<RegisteredNpcData> residents, int nextResidentId);
    bool TryGetResidentById(int residentId, out RegisteredNpcData resident);
    void AddResident(RegisteredNpcData resident);
    int AllocateResidentId();
    void ClearResidents();

    ResidentRuntimeState GetRuntimeState(int residentId);
    void MarkResidentSpawning(int residentId);
    void MarkResidentMissing(int residentId);
    void BindResident(int residentId, Character character);
    void UnbindResident(int residentId);
    bool TryDespawnResident(int residentId);
    bool TryGetResidentId(Character character, out int residentId);
    bool TryGetBoundCharacter(int residentId, out Character character);
    bool TryCaptureBoundResidentTransform(int residentId, out Vector3 worldPosition, out float worldYawDegrees, out bool isAttached);
    bool TrySpawnResident(RegisteredNpcData resident, Vector3 spawnPosition, Quaternion spawnRotation, out Character character);
}
