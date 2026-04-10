using Wyrdrasil.Core.Tool;

namespace Wyrdrasil.Souls.Tool;

public readonly struct ResidentAssignmentData
{
    public ResidentAssignmentPurpose Purpose { get; }
    public OccupationTargetRef Target { get; }

    public ResidentAssignmentData(ResidentAssignmentPurpose purpose, OccupationTargetRef target)
    {
        Purpose = purpose;
        Target = target;
    }
}
