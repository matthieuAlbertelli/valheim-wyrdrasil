using Wyrdrasil.Core.Tool;

namespace Wyrdrasil.Routines.Occupations;

/// <summary>
/// Central factory for occupation targets backed by an anchor definition.
///
/// Target sources should resolve game-specific data, then hand the normalized anchor
/// definition to this factory. This keeps the seat / bed / workbench / slot sources from
/// duplicating pose-plan construction and gives future modules a single extension point.
/// </summary>
public sealed class OccupationTargetFactory
{
    private readonly AnchorOccupationPlanBuilder _planBuilder;

    public OccupationTargetFactory(AnchorOccupationPlanBuilder planBuilder)
    {
        _planBuilder = planBuilder;
    }

    public OccupationTarget CreateAnchoredTarget(
        OccupationTargetRef reference,
        string displayName,
        int buildingId,
        int? zoneId,
        OccupationAnchorDefinition anchorDefinition,
        OccupationExecutionProfile executionProfile)
    {
        return new OccupationTarget(
            reference,
            displayName,
            buildingId,
            zoneId,
            _planBuilder.BuildPlan(anchorDefinition),
            executionProfile);
    }
}
