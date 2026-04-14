using UnityEngine;
using Wyrdrasil.Core.Tool;
using Wyrdrasil.Settlements.Tool;

namespace Wyrdrasil.Registry.UI;

public sealed class RegistryHudState
{
    public RegistryHudState(
        RegistryToolState toolState,
        string interactionModeName,
        KeyCode toggleKey,
        KeyCode nextCategoryKey,
        KeyCode nextActionKey,
        int zoneCount,
        int waypointCount,
        int? pendingLinkStartWaypointId,
        int slotCount,
        int seatCount,
        int bedCount,
        int residentCount,
        PendingZoneAuthoringSnapshot? pendingZoneAuthoring,
        string worldClockLabel,
        string worldClockModeLabel,
        bool isCraftAnchorEditorActive,
        string craftAnchorEditorStatus,
        string craftAnchorEditorControls,
        bool isConstructionPlacementPreviewActive,
        string constructionPlacementPreviewStatus,
        string constructionPlacementPreviewControls)
    {
        ToolState = toolState;
        InteractionModeName = interactionModeName;
        ToggleKey = toggleKey;
        NextCategoryKey = nextCategoryKey;
        NextActionKey = nextActionKey;
        ZoneCount = zoneCount;
        WaypointCount = waypointCount;
        PendingLinkStartWaypointId = pendingLinkStartWaypointId;
        SlotCount = slotCount;
        SeatCount = seatCount;
        BedCount = bedCount;
        ResidentCount = residentCount;
        PendingZoneAuthoring = pendingZoneAuthoring;
        WorldClockLabel = worldClockLabel;
        WorldClockModeLabel = worldClockModeLabel;
        IsCraftAnchorEditorActive = isCraftAnchorEditorActive;
        CraftAnchorEditorStatus = craftAnchorEditorStatus;
        CraftAnchorEditorControls = craftAnchorEditorControls;
        IsConstructionPlacementPreviewActive = isConstructionPlacementPreviewActive;
        ConstructionPlacementPreviewStatus = constructionPlacementPreviewStatus;
        ConstructionPlacementPreviewControls = constructionPlacementPreviewControls;
    }

    public RegistryToolState ToolState { get; }
    public string InteractionModeName { get; }
    public KeyCode ToggleKey { get; }
    public KeyCode NextCategoryKey { get; }
    public KeyCode NextActionKey { get; }
    public int ZoneCount { get; }
    public int WaypointCount { get; }
    public int? PendingLinkStartWaypointId { get; }
    public int SlotCount { get; }
    public int SeatCount { get; }
    public int BedCount { get; }
    public int ResidentCount { get; }
    public PendingZoneAuthoringSnapshot? PendingZoneAuthoring { get; }
    public string WorldClockLabel { get; }
    public string WorldClockModeLabel { get; }
    public bool IsCraftAnchorEditorActive { get; }
    public string CraftAnchorEditorStatus { get; }
    public string CraftAnchorEditorControls { get; }
    public bool IsConstructionPlacementPreviewActive { get; }
    public string ConstructionPlacementPreviewStatus { get; }
    public string ConstructionPlacementPreviewControls { get; }
}
