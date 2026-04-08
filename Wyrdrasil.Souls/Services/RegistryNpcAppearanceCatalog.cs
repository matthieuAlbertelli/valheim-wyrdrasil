using System.Collections.Generic;
using UnityEngine;

namespace Wyrdrasil.Registry.Services;

public sealed class RegistryNpcAppearanceCatalog
{
    public IReadOnlyList<int> ModelIndices { get; } = new[] { 0, 1 };

    public IReadOnlyList<string> MaleHairItems { get; } = new[]
    {
        "Hair1",
        "Hair2",
        "Hair3",
        "Hair4",
        "Hair5",
        "Hair6"
    };

    public IReadOnlyList<string> FemaleHairItems { get; } = new[]
    {
        "Hair1",
        "Hair2",
        "Hair3",
        "Hair4",
        "Hair5",
        "Hair6"
    };

    public IReadOnlyList<string?> MaleBeardItems { get; } = new string?[]
    {
        null,
        "Beard1",
        "Beard2",
        "Beard3",
        "Beard4"
    };

    public IReadOnlyList<string?> FemaleBeardItems { get; } = new string?[]
    {
        null
    };

    public IReadOnlyList<Color> SkinColors { get; } = new[]
    {
        new Color(0.93f, 0.80f, 0.67f),
        new Color(0.84f, 0.69f, 0.56f),
        new Color(0.72f, 0.56f, 0.44f),
        new Color(0.58f, 0.43f, 0.33f)
    };

    public IReadOnlyList<Color> HairColors { get; } = new[]
    {
        new Color(0.88f, 0.76f, 0.42f),
        new Color(0.56f, 0.34f, 0.17f),
        new Color(0.18f, 0.12f, 0.09f),
        new Color(0.67f, 0.67f, 0.67f),
        new Color(0.78f, 0.54f, 0.20f)
    };
}
