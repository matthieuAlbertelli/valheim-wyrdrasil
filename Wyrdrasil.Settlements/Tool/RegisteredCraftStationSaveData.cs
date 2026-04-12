using System;
using Wyrdrasil.Core.Tool;

namespace Wyrdrasil.Settlements.Tool;

[Serializable]
public sealed class RegisteredCraftStationSaveData
{
    public int Id;
    public int BuildingId;
    public int ZoneId;
    public string DisplayName = string.Empty;
    public string PersistentFurnitureId = string.Empty;
    public Float3SaveData AnchorLocalPosition = new();
    public Float3SaveData AnchorLocalForward = new();
    public string InteractionProfileId = string.Empty;
    public Float3SaveData ReferenceWorldPosition = new();
    public int? AssignedRegisteredNpcId;
}
