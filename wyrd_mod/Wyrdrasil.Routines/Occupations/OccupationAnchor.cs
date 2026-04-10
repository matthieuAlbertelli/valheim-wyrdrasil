using UnityEngine;

namespace Wyrdrasil.Routines.Occupations;

public readonly struct OccupationAnchor
{
    public Vector3 ApproachPosition { get; }
    public Vector3 UsePosition { get; }
    public Vector3 FacingDirection { get; }

    public OccupationAnchor(Vector3 approachPosition, Vector3 usePosition, Vector3 facingDirection)
    {
        ApproachPosition = approachPosition;
        UsePosition = usePosition;
        FacingDirection = facingDirection;
    }
}
