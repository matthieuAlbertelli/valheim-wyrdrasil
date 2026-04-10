using System;
using Wyrdrasil.Core.Tool;

namespace Wyrdrasil.Souls.Tool;

[Serializable]
public sealed class ResidentAssignmentSaveData
{
    public ResidentAssignmentPurpose Purpose;
    public OccupationTargetKind TargetKind;
    public int TargetId;
}
