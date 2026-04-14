using UnityEngine;

namespace Wyrdrasil.Core.Tool;

public readonly struct OccupationAnchorPose
{
    public Vector3 EngagePosition { get; }
    public Vector3 FacingDirection { get; }

    public OccupationAnchorPose(Vector3 engagePosition, Vector3 facingDirection)
    {
        EngagePosition = engagePosition;
        FacingDirection = facingDirection;
    }
}
