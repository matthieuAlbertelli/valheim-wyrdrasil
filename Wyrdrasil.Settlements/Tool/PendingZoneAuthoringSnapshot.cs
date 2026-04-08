namespace Wyrdrasil.Settlements.Tool;


public sealed class PendingZoneAuthoringSnapshot
{
    public ZoneAuthoringPhase Phase { get; }
    public int PointCount { get; }
    public float BaseY { get; }
    public float TopY { get; }
    public bool CanCloseFootprint { get; }

    public PendingZoneAuthoringSnapshot(
        ZoneAuthoringPhase phase,
        int pointCount,
        float baseY,
        float topY,
        bool canCloseFootprint)
    {
        Phase = phase;
        PointCount = pointCount;
        BaseY = baseY;
        TopY = topY;
        CanCloseFootprint = canCloseFootprint;
    }
}
