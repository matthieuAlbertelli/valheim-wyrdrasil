using UnityEngine;

namespace Wyrdrasil.Core.Tool;

/// <summary>
/// Declarative description of an occupation anchor.
///
/// This type is intentionally data-only. Feature modules can describe a chair, bed,
/// workbench, forge, counter, guard post, construction post or any future anchor with
/// the same shape, while runtime modules remain responsible for navigation, docking,
/// native attachment and animation.
/// </summary>
public readonly struct OccupationAnchorDefinition
{
    public OccupationAnchorAttachmentKind AttachmentKind { get; }
    public Vector3 ApproachPosition { get; }
    public OccupationAnchorPose Pose { get; }
    public bool UsesExplicitApproachPosition { get; }
    public float ApproachDistance { get; }
    public float NavigationStopDistance { get; }
    public float EngageRadius { get; }
    public float SustainRadius { get; }
    public OccupationAnchorApproachProfile ApproachProfile { get; }

    private OccupationAnchorDefinition(
        OccupationAnchorAttachmentKind attachmentKind,
        OccupationAnchorPose pose,
        Vector3 approachPosition,
        bool usesExplicitApproachPosition,
        float approachDistance,
        float navigationStopDistance,
        float engageRadius,
        float sustainRadius,
        OccupationAnchorApproachProfile approachProfile)
    {
        AttachmentKind = attachmentKind;
        Pose = pose;
        ApproachPosition = approachPosition;
        UsesExplicitApproachPosition = usesExplicitApproachPosition;
        ApproachDistance = Mathf.Max(0f, approachDistance);
        NavigationStopDistance = Mathf.Max(0.01f, navigationStopDistance);
        EngageRadius = Mathf.Max(0.01f, engageRadius);
        SustainRadius = Mathf.Max(0.01f, sustainRadius);
        ApproachProfile = approachProfile;
    }

    public static OccupationAnchorDefinition FromExplicitPositions(
        OccupationAnchorAttachmentKind attachmentKind,
        Vector3 approachPosition,
        Vector3 engagePosition,
        Vector3 facingDirection,
        float navigationStopDistance,
        float engageRadius,
        float sustainRadius,
        OccupationAnchorApproachProfile approachProfile)
    {
        return new OccupationAnchorDefinition(
            attachmentKind,
            new OccupationAnchorPose(engagePosition, facingDirection),
            approachPosition,
            usesExplicitApproachPosition: true,
            approachDistance: 0f,
            navigationStopDistance: navigationStopDistance,
            engageRadius: engageRadius,
            sustainRadius: sustainRadius,
            approachProfile: approachProfile);
    }

    public static OccupationAnchorDefinition FromPose(
        OccupationAnchorAttachmentKind attachmentKind,
        Vector3 engagePosition,
        Vector3 facingDirection,
        float approachDistance,
        float navigationStopDistance,
        float engageRadius,
        float sustainRadius,
        OccupationAnchorApproachProfile approachProfile)
    {
        return new OccupationAnchorDefinition(
            attachmentKind,
            new OccupationAnchorPose(engagePosition, facingDirection),
            Vector3.zero,
            usesExplicitApproachPosition: false,
            approachDistance: approachDistance,
            navigationStopDistance: navigationStopDistance,
            engageRadius: engageRadius,
            sustainRadius: sustainRadius,
            approachProfile: approachProfile);
    }
}
