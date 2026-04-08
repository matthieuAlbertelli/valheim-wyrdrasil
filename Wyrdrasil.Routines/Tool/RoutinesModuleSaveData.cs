using System;

namespace Wyrdrasil.Registry.Tool;

[Serializable]
public sealed class RoutinesModuleSaveData
{
    public int SchemaVersion = 1;
    public bool HasSimulatedMinuteOfDay;
    public int SimulatedMinuteOfDay;
}
