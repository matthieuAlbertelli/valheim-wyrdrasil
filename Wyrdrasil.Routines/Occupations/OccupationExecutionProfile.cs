using UnityEngine;
using Wyrdrasil.Core.Tool;

namespace Wyrdrasil.Routines.Occupations;

public sealed class OccupationExecutionProfile
{
    public enum OccupationExecutionKind
    {
        Stand,
        Seat,
        Bed,
        CraftStation
    }

    public const string StandStrategyId = "stand";
    public const string SeatStrategyId = "seat";
    public const string BedStrategyId = "bed";
    public const string ApproachNavigationStrategyId = "approach";
    public const string AnchoredStandLifecycleStrategyId = "anchored-stand.lifecycle";
    public const string WorkbenchLifecycleStrategyId = "workbench.lifecycle";
    public const string CraftStationLifecycleStrategyId = WorkbenchLifecycleStrategyId;
    public const string CraftStationSustainStrategyId = "craftstation.sustain";
    public const string ConstructionWorkSustainStrategyId = "construction.work";

    public string NavigationStrategyId { get; }
    public string LifecycleStrategyId { get; }
    public string SustainStrategyId { get; }
    public OccupationExecutionKind Kind { get; }
    public OccupationAnchorAttachmentKind AnchorKind { get; }
    public Chair? ChairComponent { get; }
    public Bed? BedComponent { get; }
    public Transform? AttachPoint { get; }
    public Interactable? Interactable { get; }
    public OccupationAnchorDefinition? AnchorDefinition { get; }
    public OccupationAnchorApproachProfile AnchorApproachProfile { get; }

    private OccupationExecutionProfile(
        string navigationStrategyId,
        string lifecycleStrategyId,
        string sustainStrategyId,
        OccupationExecutionKind kind,
        OccupationAnchorAttachmentKind anchorKind = OccupationAnchorAttachmentKind.None,
        Chair? chairComponent = null,
        Bed? bedComponent = null,
        Transform? attachPoint = null,
        Interactable? interactable = null,
        OccupationAnchorDefinition? anchorDefinition = null,
        OccupationAnchorApproachProfile? anchorApproachProfile = null)
    {
        NavigationStrategyId = navigationStrategyId;
        LifecycleStrategyId = lifecycleStrategyId;
        SustainStrategyId = sustainStrategyId;
        Kind = kind;
        AnchorKind = anchorKind;
        ChairComponent = chairComponent;
        BedComponent = bedComponent;
        AttachPoint = attachPoint;
        Interactable = interactable;
        AnchorDefinition = anchorDefinition;
        AnchorApproachProfile = anchorApproachProfile ?? anchorDefinition?.ApproachProfile ?? ResolveDefaultAnchorApproachProfile(kind);
    }

    public bool IsStand => Kind == OccupationExecutionKind.Stand;
    public bool IsSeat => Kind == OccupationExecutionKind.Seat;
    public bool IsBed => Kind == OccupationExecutionKind.Bed;
    public bool IsCraftStation => Kind == OccupationExecutionKind.CraftStation;
    public bool HasAttachmentAnchor => AnchorKind is OccupationAnchorAttachmentKind.Seat or OccupationAnchorAttachmentKind.Bed;
    public bool HasAnchorDefinition => AnchorDefinition.HasValue;

    public static OccupationExecutionProfile Stand()
    {
        return new OccupationExecutionProfile(StandStrategyId, StandStrategyId, StandStrategyId, OccupationExecutionKind.Stand);
    }

    public static OccupationExecutionProfile Stand(OccupationAnchorDefinition anchorDefinition)
    {
        return new OccupationExecutionProfile(
            StandStrategyId,
            StandStrategyId,
            StandStrategyId,
            OccupationExecutionKind.Stand,
            anchorDefinition: anchorDefinition,
            anchorApproachProfile: anchorDefinition.ApproachProfile);
    }

    public static OccupationExecutionProfile AnchoredStand(string sustainStrategyId)
    {
        return new OccupationExecutionProfile(ApproachNavigationStrategyId, AnchoredStandLifecycleStrategyId, sustainStrategyId, OccupationExecutionKind.Stand);
    }

    public static OccupationExecutionProfile AnchoredStand(string sustainStrategyId, OccupationAnchorDefinition anchorDefinition)
    {
        return new OccupationExecutionProfile(
            ApproachNavigationStrategyId,
            AnchoredStandLifecycleStrategyId,
            sustainStrategyId,
            OccupationExecutionKind.Stand,
            anchorDefinition: anchorDefinition,
            anchorApproachProfile: anchorDefinition.ApproachProfile);
    }

    public static OccupationExecutionProfile Seat(Chair? chairComponent)
    {
        return new OccupationExecutionProfile(
            SeatStrategyId,
            SeatStrategyId,
            SeatStrategyId,
            OccupationExecutionKind.Seat,
            OccupationAnchorAttachmentKind.Seat,
            chairComponent: chairComponent,
            anchorApproachProfile: OccupationAnchorApproachProfile.SeatDefault);
    }

    public static OccupationExecutionProfile Seat(Chair? chairComponent, OccupationAnchorDefinition anchorDefinition)
    {
        return new OccupationExecutionProfile(
            SeatStrategyId,
            SeatStrategyId,
            SeatStrategyId,
            OccupationExecutionKind.Seat,
            OccupationAnchorAttachmentKind.Seat,
            chairComponent: chairComponent,
            anchorDefinition: anchorDefinition,
            anchorApproachProfile: anchorDefinition.ApproachProfile);
    }

    public static OccupationExecutionProfile Bed(Bed? bedComponent, Transform? attachPoint)
    {
        return new OccupationExecutionProfile(
            BedStrategyId,
            BedStrategyId,
            BedStrategyId,
            OccupationExecutionKind.Bed,
            OccupationAnchorAttachmentKind.Bed,
            bedComponent: bedComponent,
            attachPoint: attachPoint,
            anchorApproachProfile: OccupationAnchorApproachProfile.BedDefault);
    }

    public static OccupationExecutionProfile Bed(Bed? bedComponent, Transform? attachPoint, OccupationAnchorDefinition anchorDefinition)
    {
        return new OccupationExecutionProfile(
            BedStrategyId,
            BedStrategyId,
            BedStrategyId,
            OccupationExecutionKind.Bed,
            OccupationAnchorAttachmentKind.Bed,
            bedComponent: bedComponent,
            attachPoint: attachPoint,
            anchorDefinition: anchorDefinition,
            anchorApproachProfile: anchorDefinition.ApproachProfile);
    }

    public static OccupationExecutionProfile Workbench(string sustainStrategyId, Interactable? interactable)
    {
        return new OccupationExecutionProfile(
            ApproachNavigationStrategyId,
            WorkbenchLifecycleStrategyId,
            sustainStrategyId,
            OccupationExecutionKind.CraftStation,
            interactable: interactable,
            anchorApproachProfile: OccupationAnchorApproachProfile.WorkPointDefault);
    }

    public static OccupationExecutionProfile Workbench(
        string sustainStrategyId,
        Interactable? interactable,
        OccupationAnchorDefinition anchorDefinition)
    {
        return new OccupationExecutionProfile(
            ApproachNavigationStrategyId,
            WorkbenchLifecycleStrategyId,
            sustainStrategyId,
            OccupationExecutionKind.CraftStation,
            interactable: interactable,
            anchorDefinition: anchorDefinition,
            anchorApproachProfile: anchorDefinition.ApproachProfile);
    }

    public static OccupationExecutionProfile CraftStation(Interactable? interactable)
    {
        return Workbench(CraftStationSustainStrategyId, interactable);
    }

    public static OccupationExecutionProfile CraftStation(Interactable? interactable, OccupationAnchorDefinition anchorDefinition)
    {
        return Workbench(CraftStationSustainStrategyId, interactable, anchorDefinition);
    }

    public static OccupationExecutionProfile ConstructionWork(Interactable? interactable)
    {
        return Workbench(ConstructionWorkSustainStrategyId, interactable);
    }

    public static OccupationExecutionProfile ConstructionWork(Interactable? interactable, OccupationAnchorDefinition anchorDefinition)
    {
        return Workbench(ConstructionWorkSustainStrategyId, interactable, anchorDefinition);
    }

    private static OccupationAnchorApproachProfile ResolveDefaultAnchorApproachProfile(OccupationExecutionKind kind)
    {
        return kind switch
        {
            OccupationExecutionKind.Seat => OccupationAnchorApproachProfile.SeatDefault,
            OccupationExecutionKind.Bed => OccupationAnchorApproachProfile.BedDefault,
            OccupationExecutionKind.CraftStation => OccupationAnchorApproachProfile.WorkPointDefault,
            OccupationExecutionKind.Stand => OccupationAnchorApproachProfile.StandingPointDefault,
            _ => OccupationAnchorApproachProfile.StandingPointDefault
        };
    }
}
