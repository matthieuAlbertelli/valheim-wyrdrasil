using UnityEngine;
using Wyrdrasil.Core.Tool;

namespace Wyrdrasil.Routines.Occupations;

public sealed class OccupationTarget
{
    public OccupationTargetRef Reference { get; }
    public string DisplayName { get; }
    public int BuildingId { get; }
    public int? ZoneId { get; }
    public OccupationAnchor Anchor { get; }
    public OccupationExecutionProfile Execution { get; }

    public OccupationTarget(
        OccupationTargetRef reference,
        string displayName,
        int buildingId,
        int? zoneId,
        OccupationAnchor anchor,
        OccupationExecutionProfile execution)
    {
        Reference = reference;
        DisplayName = displayName;
        BuildingId = buildingId;
        ZoneId = zoneId;
        Anchor = anchor;
        Execution = execution;
    }
}
