using UnityEngine;

namespace Wyrdrasil.Routines.Occupations;

public sealed class OccupationTarget
{
    public OccupationTargetRef Reference { get; }
    public string DisplayName { get; }
    public int BuildingId { get; }
    public int? ZoneId { get; }
    public OccupationAnchor Anchor { get; }
    public OccupationUseMode UseMode { get; }
    public Chair? ChairComponent { get; }
    public Bed? BedComponent { get; }
    public Transform? AttachPoint { get; }

    public OccupationTarget(
        OccupationTargetRef reference,
        string displayName,
        int buildingId,
        int? zoneId,
        OccupationAnchor anchor,
        OccupationUseMode useMode,
        Chair? chairComponent = null,
        Bed? bedComponent = null,
        Transform? attachPoint = null)
    {
        Reference = reference;
        DisplayName = displayName;
        BuildingId = buildingId;
        ZoneId = zoneId;
        Anchor = anchor;
        UseMode = useMode;
        ChairComponent = chairComponent;
        BedComponent = bedComponent;
        AttachPoint = attachPoint;
    }
}
