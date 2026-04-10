namespace Wyrdrasil.Routines.Occupations;

public readonly struct OccupationTargetRef
{
    public OccupationTargetKind TargetKind { get; }
    public int TargetId { get; }

    public OccupationTargetRef(OccupationTargetKind targetKind, int targetId)
    {
        TargetKind = targetKind;
        TargetId = targetId;
    }

    public override string ToString()
    {
        return $"{TargetKind}#{TargetId}";
    }
}
