using System;

namespace Wyrdrasil.Construction.Models;

public sealed class ConstructionProjectProgressData
{
    public int TotalPieceCount { get; set; }
    public int BuiltPieceCount { get; set; }
    public float AccumulatedPieceWork { get; set; }

    public int RemainingPieceCount => Math.Max(0, TotalPieceCount - BuiltPieceCount);
}
