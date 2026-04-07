using System;
using UnityEngine;

namespace Wyrdrasil.Registry.Tool;

[Serializable]
public sealed class Float3SaveData
{
    public float X;
    public float Y;
    public float Z;

    public static Float3SaveData FromVector3(Vector3 value)
    {
        return new Float3SaveData { X = value.x, Y = value.y, Z = value.z };
    }

    public Vector3 ToVector3()
    {
        return new Vector3(X, Y, Z);
    }
}
