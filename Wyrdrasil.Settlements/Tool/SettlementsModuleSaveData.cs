using System;
using System.Collections.Generic;

namespace Wyrdrasil.Settlements.Tool;


[Serializable]
public sealed class SettlementsModuleSaveData
{
    public int SchemaVersion = 2;
    public int NextBuildingId = 1;
    public int NextZoneId = 1;
    public int NextWaypointId = 1;
    public int NextSlotId = 1;
    public int NextSeatId = 1;
    public int NextBedId = 1;
    public int NextCraftStationId = 1;
    public List<BuildingSaveData> Buildings = new();
    public List<FunctionalZoneSaveData> Zones = new();
    public List<NavigationWaypointSaveData> Waypoints = new();
    public List<NavigationWaypointLinkSaveData> WaypointLinks = new();
    public List<ZoneSlotSaveData> Slots = new();
    public List<RegisteredSeatSaveData> Seats = new();
    public List<RegisteredBedSaveData> Beds = new();
    public List<RegisteredCraftStationSaveData> CraftStations = new();
}
