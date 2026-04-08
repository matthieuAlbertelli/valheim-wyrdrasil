using System;
using UnityEngine;

namespace Wyrdrasil.Core.Tool;


[Serializable]
public sealed class ColorSaveData
{
    public float R;
    public float G;
    public float B;
    public float A;

    public static ColorSaveData FromColor(Color value)
    {
        return new ColorSaveData { R = value.r, G = value.g, B = value.b, A = value.a };
    }

    public Color ToColor()
    {
        return new Color(R, G, B, A);
    }
}
