using System.Threading;
using Wyrdrasil.Registry.Tool;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Souls.Services;


public sealed class NpcIdentityGenerator
{
    private readonly NpcAppearanceGenerator _appearanceGenerator;
    private readonly NpcEquipmentGenerator _equipmentGenerator;
    private int _nextSeed = System.Environment.TickCount;

    public NpcIdentityGenerator(
        NpcAppearanceGenerator appearanceGenerator,
        NpcEquipmentGenerator equipmentGenerator)
    {
        _appearanceGenerator = appearanceGenerator;
        _equipmentGenerator = equipmentGenerator;
    }

    public VikingIdentityData Generate(NpcRole role)
    {
        var seed = Interlocked.Increment(ref _nextSeed);
        return Generate(seed, role);
    }

    public VikingIdentityData Generate(int seed, NpcRole role)
    {
        var appearance = _appearanceGenerator.Generate(seed, role);
        var equipment = _equipmentGenerator.Generate(seed, role);
        return new VikingIdentityData(seed, role, appearance, equipment);
    }
}
