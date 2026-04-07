using System;
using System.Collections.Generic;

namespace Wyrdrasil.Registry.Tool;

[Serializable]
public sealed class RegistrySaveData
{
    public int Version = 1;
    public int NextBuildingId = 1;
    public int NextZoneId = 1;
    public int NextSlotId = 1;
    public int NextSeatId = 1;
    public int NextResidentId = 1;
    public List<BuildingSaveData> Buildings = new();
    public List<FunctionalZoneSaveData> Zones = new();
    public List<ZoneSlotSaveData> Slots = new();
    public List<RegisteredSeatSaveData> Seats = new();
    public List<RegisteredNpcSaveData> Residents = new();
}
