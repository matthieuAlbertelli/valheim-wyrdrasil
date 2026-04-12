using UnityEngine;

namespace Wyrdrasil.Settlements.Tool;

public readonly struct InteractionAnchorPose
{
    public Vector3 LocalPosition { get; }
    public Vector3 LocalForward { get; }

    public InteractionAnchorPose(Vector3 localPosition, Vector3 localForward)
    {
        LocalPosition = localPosition;
        localForward.y = 0f;
        LocalForward = localForward.sqrMagnitude > 0.0001f
            ? localForward.normalized
            : Vector3.forward;
    }
}
