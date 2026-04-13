namespace Wyrdrasil.Construction.Models;

public sealed class ConstructionPieceProgressData
{
    public int PieceId { get; set; }
    public bool MaterialsReserved { get; set; }
    public float CurrentWork { get; set; }
    public float RequiredWork { get; set; } = 1f;
    public bool IsBuilt { get; set; }
}
