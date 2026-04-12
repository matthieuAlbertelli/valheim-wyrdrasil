using Wyrdrasil.Core.Tool;

namespace Wyrdrasil.Routines.Occupations;

public sealed class OccupationTarget
{
    public OccupationTargetRef Reference { get; }
    public string DisplayName { get; }
    public int BuildingId { get; }
    public int? ZoneId { get; }
    public OccupationPosePlan Plan { get; }
    public OccupationExecutionProfile Execution { get; }

    public OccupationTarget(
        OccupationTargetRef reference,
        string displayName,
        int buildingId,
        int? zoneId,
        OccupationPosePlan plan,
        OccupationExecutionProfile execution)
    {
        Reference = reference;
        DisplayName = displayName;
        BuildingId = buildingId;
        ZoneId = zoneId;
        Plan = plan;
        Execution = execution;
    }
}
