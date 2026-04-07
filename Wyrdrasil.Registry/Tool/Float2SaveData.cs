using System;
using UnityEngine;

namespace Wyrdrasil.Registry.Tool;

[Serializable]
public sealed class Float2SaveData
{
    public float X;
    public float Y;

    public static Float2SaveData FromVector2(Vector2 value)
    {
        return new Float2SaveData { X = value.x, Y = value.y };
    }

    public Vector2 ToVector2()
    {
        return new Vector2(X, Y);
    }
}
