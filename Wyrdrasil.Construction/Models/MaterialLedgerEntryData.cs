namespace Wyrdrasil.Construction.Models;

public sealed class MaterialLedgerEntryData
{
    public string ItemPrefabName { get; set; } = string.Empty;
    public int RequiredAmount { get; set; }
    public int DeliveredAmount { get; set; }
    public int ConsumedAmount { get; set; }
}
