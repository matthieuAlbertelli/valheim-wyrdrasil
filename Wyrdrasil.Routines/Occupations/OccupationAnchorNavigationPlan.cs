using UnityEngine;
using Wyrdrasil.Core.Tool;

namespace Wyrdrasil.Routines.Occupations;

/// <summary>
/// Runtime navigation handoff for occupations that end by attaching to a concrete world anchor.
///
/// Seat and bed occupations currently use this shape. Future anchors such as stools, thrones,
/// benches or scripted work positions should be able to reuse the same navigation / close-range
/// forcing pipeline without adding another parallel route controller branch.
/// </summary>
public readonly struct OccupationAnchorNavigationPlan
{
    public OccupationAnchorAttachmentKind AnchorKind { get; }
    public Vector3 ApproachPosition { get; }
    public Vector3 EngagePosition { get; }
    public Vector3 FacingDirection { get; }
    public OccupationAnchorApproachProfile ApproachProfile { get; }
    public Chair? ChairComponent { get; }
    public Bed? BedComponent { get; }
    public Transform? AttachPoint { get; }

    private OccupationAnchorNavigationPlan(
        OccupationAnchorAttachmentKind anchorKind,
        Vector3 approachPosition,
        Vector3 engagePosition,
        Vector3 facingDirection,
        OccupationAnchorApproachProfile approachProfile,
        Chair? chairComponent = null,
        Bed? bedComponent = null,
        Transform? attachPoint = null)
    {
        AnchorKind = anchorKind;
        ApproachPosition = approachPosition;
        EngagePosition = engagePosition;
        FacingDirection = facingDirection.sqrMagnitude > 0.0001f ? facingDirection.normalized : Vector3.forward;
        ApproachProfile = approachProfile;
        ChairComponent = chairComponent;
        BedComponent = bedComponent;
        AttachPoint = attachPoint;
    }

    public static OccupationAnchorNavigationPlan FromTarget(OccupationTarget target)
    {
        return target.Execution.AnchorKind switch
        {
            OccupationAnchorAttachmentKind.Seat => Seat(
                target.Plan.ApproachPosition,
                target.Plan.EngagePosition,
                target.Plan.FacingDirection,
                target.Execution.ChairComponent,
                target.Execution.AnchorApproachProfile),

            OccupationAnchorAttachmentKind.Bed => Bed(
                target.Plan.ApproachPosition,
                target.Plan.EngagePosition,
                target.Plan.FacingDirection,
                target.Execution.BedComponent,
                target.Execution.AttachPoint,
                target.Execution.AnchorApproachProfile),

            _ => None(
                target.Plan.ApproachPosition,
                target.Plan.EngagePosition,
                target.Plan.FacingDirection,
                target.Execution.AnchorApproachProfile)
        };
    }

    public static OccupationAnchorNavigationPlan Seat(
        Vector3 approachPosition,
        Vector3 engagePosition,
        Vector3 facingDirection,
        Chair? chairComponent,
        OccupationAnchorApproachProfile approachProfile)
    {
        return new OccupationAnchorNavigationPlan(
            OccupationAnchorAttachmentKind.Seat,
            approachPosition,
            engagePosition,
            facingDirection,
            approachProfile,
            chairComponent: chairComponent);
    }

    public static OccupationAnchorNavigationPlan Bed(
        Vector3 approachPosition,
        Vector3 engagePosition,
        Vector3 facingDirection,
        Bed? bedComponent,
        Transform? attachPoint,
        OccupationAnchorApproachProfile approachProfile)
    {
        return new OccupationAnchorNavigationPlan(
            OccupationAnchorAttachmentKind.Bed,
            approachPosition,
            engagePosition,
            facingDirection,
            approachProfile,
            bedComponent: bedComponent,
            attachPoint: attachPoint);
    }

    private static OccupationAnchorNavigationPlan None(
        Vector3 approachPosition,
        Vector3 engagePosition,
        Vector3 facingDirection,
        OccupationAnchorApproachProfile approachProfile)
    {
        return new OccupationAnchorNavigationPlan(
            OccupationAnchorAttachmentKind.None,
            approachPosition,
            engagePosition,
            facingDirection,
            approachProfile);
    }
}
