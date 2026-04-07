using System;
using System.Collections.Generic;
using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Services;

public sealed class RegistryNpcEquipmentGenerator
{
    private readonly RegistryNpcEquipmentCatalog _catalog;

    public RegistryNpcEquipmentGenerator(RegistryNpcEquipmentCatalog catalog)
    {
        _catalog = catalog;
    }

    public VikingEquipmentData Generate(int seed, NpcRole role)
    {
        var random = new Random(unchecked(seed * 397) ^ (int)role);
        var pool = GetPool(role);
        return pool[random.Next(pool.Count)];
    }

    private IReadOnlyList<VikingEquipmentData> GetPool(NpcRole role)
    {
        return role switch
        {
            NpcRole.Innkeeper => _catalog.InnkeeperLoadouts,
            NpcRole.Guard => _catalog.GuardLoadouts,
            NpcRole.Blacksmith => _catalog.BlacksmithLoadouts,
            _ => _catalog.VillagerLoadouts
        };
    }
}
