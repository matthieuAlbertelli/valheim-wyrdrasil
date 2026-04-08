using System.Collections.Generic;
using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Services;

public sealed class RegistryResidentCatalogService
{
    private readonly List<RegisteredNpcData> _registeredNpcs = new();
    private readonly Dictionary<int, RegisteredNpcData> _registeredById = new();

    private int _nextRegisteredNpcId = 1;

    public IReadOnlyList<RegisteredNpcData> RegisteredNpcs => _registeredNpcs;
    public int NextRegisteredNpcId => _nextRegisteredNpcId;

    public void LoadResidents(IEnumerable<RegisteredNpcData> residents, int nextResidentId)
    {
        _registeredNpcs.Clear();
        _registeredById.Clear();

        foreach (var resident in residents)
        {
            AddResident(resident);
        }

        _nextRegisteredNpcId = nextResidentId;
    }

    public bool TryGetResidentById(int residentId, out RegisteredNpcData resident)
    {
        return _registeredById.TryGetValue(residentId, out resident!);
    }

    public void AddResident(RegisteredNpcData resident)
    {
        _registeredNpcs.Add(resident);
        _registeredById[resident.Id] = resident;
    }

    public int AllocateResidentId()
    {
        return _nextRegisteredNpcId++;
    }

    public void Clear()
    {
        _registeredNpcs.Clear();
        _registeredById.Clear();
        _nextRegisteredNpcId = 1;
    }
}
