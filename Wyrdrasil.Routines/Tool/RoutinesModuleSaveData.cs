using System;

namespace Wyrdrasil.Routines.Tool;


[Serializable]
public sealed class RoutinesModuleSaveData
{
    public int SchemaVersion = 1;
    public bool HasSimulatedMinuteOfDay;
    public int SimulatedMinuteOfDay;
}
