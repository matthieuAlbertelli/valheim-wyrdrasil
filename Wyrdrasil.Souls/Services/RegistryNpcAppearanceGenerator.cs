using System.Collections.Generic;
using UnityEngine;
using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Services;

public sealed class RegistryNpcAppearanceGenerator
{
    private readonly RegistryNpcAppearanceCatalog _catalog;

    public RegistryNpcAppearanceGenerator(RegistryNpcAppearanceCatalog catalog)
    {
        _catalog = catalog;
    }

    public VikingAppearanceData Generate(int seed, NpcRole role)
    {
        var random = new System.Random(seed);
        var modelIndex = Pick(_catalog.ModelIndices, random);
        var isFemale = modelIndex == 1;
        var hairItem = Pick(isFemale ? _catalog.FemaleHairItems : _catalog.MaleHairItems, random);
        var beardItem = Pick(isFemale ? _catalog.FemaleBeardItems : _catalog.MaleBeardItems, random);
        var skinColor = Pick(_catalog.SkinColors, random);
        var hairColor = Pick(_catalog.HairColors, random);

        return new VikingAppearanceData(modelIndex, hairItem, beardItem, skinColor, hairColor);
    }

    private static T Pick<T>(IReadOnlyList<T> pool, System.Random random)
    {
        return pool[random.Next(pool.Count)];
    }
}
