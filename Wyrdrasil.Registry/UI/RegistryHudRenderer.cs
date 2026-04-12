using UnityEngine;
using Wyrdrasil.Registry.Tool;
using Wyrdrasil.Settlements.Tool;

namespace Wyrdrasil.Registry.UI;

public sealed class RegistryHudRenderer
{
    private GUIStyle? _titleStyle;
    private GUIStyle? _textStyle;
    private GUIStyle? _hintStyle;

    public void Draw(
        RegistryToolState state,
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
        string craftAnchorEditorControls)
    {
        if (!state.IsRegistryModeEnabled)
        {
            return;
        }

        EnsureStyles();
        var panelHeight = pendingZoneAuthoring == null ? 408f : 504f;
        var panelRect = new Rect(20f, 180f, 880f, panelHeight);

        GUI.Box(panelRect, GUIContent.none);
        GUI.Label(new Rect(35f, 190f, 320f, 24f), "Registre des Âmes", _titleStyle!);
        GUI.Label(new Rect(35f, 218f, 360f, 20f), "Mode Registre actif", _textStyle!);
        GUI.Label(new Rect(35f, 242f, 680f, 20f), worldClockLabel, _textStyle!);
        GUI.Label(new Rect(35f, 266f, 760f, 20f), worldClockModeLabel, _textStyle!);
        GUI.Label(new Rect(35f, 290f, 680f, 20f), $"Catégorie : {FormatCategory(state.SelectedCategory)}", _textStyle!);
        GUI.Label(new Rect(35f, 314f, 760f, 20f), $"Action : {FormatAction(state.SelectedAction)}", _textStyle!);
        GUI.Label(new Rect(35f, 338f, 420f, 20f), $"Zones : {zoneCount}", _textStyle!);
        GUI.Label(new Rect(35f, 362f, 420f, 20f), $"Waypoints : {waypointCount}", _textStyle!);
        GUI.Label(new Rect(35f, 386f, 540f, 20f), $"Départ de lien sélectionné : {FormatPendingLink(pendingLinkStartWaypointId)}", _textStyle!);
        GUI.Label(new Rect(35f, 410f, 420f, 20f), $"Slots aubergiste : {slotCount}", _textStyle!);
        GUI.Label(new Rect(35f, 434f, 420f, 20f), $"Sièges désignés : {seatCount}", _textStyle!);
        GUI.Label(new Rect(35f, 458f, 420f, 20f), $"Lits désignés : {bedCount}", _textStyle!);
        GUI.Label(new Rect(35f, 482f, 420f, 20f), $"PNJ enregistrés : {residentCount}", _textStyle!);
        GUI.Label(new Rect(35f, 506f, 760f, 20f), $"Résident sélectionné pour force assign : {FormatPendingResidentForceAssign(state)}", _textStyle!);

        var nextLineY = 530f;
        if (pendingZoneAuthoring != null)
        {
            GUI.Label(new Rect(35f, nextLineY, 760f, 20f), $"Création zone : {FormatZoneAuthoringPhase(pendingZoneAuthoring.Phase)}", _textStyle!);
            nextLineY += 24f;
            GUI.Label(new Rect(35f, nextLineY, 760f, 20f), $"Points : {pendingZoneAuthoring.PointCount} | Fermeture possible : {(pendingZoneAuthoring.CanCloseFootprint ? "Oui" : "Non")}", _textStyle!);
            nextLineY += 24f;
            if (pendingZoneAuthoring.Phase == ZoneAuthoringPhase.Height)
            {
                GUI.Label(new Rect(35f, nextLineY, 760f, 20f), $"BaseY : {pendingZoneAuthoring.BaseY:0.00} | TopY : {pendingZoneAuthoring.TopY:0.00}", _textStyle!);
                nextLineY += 24f;
                GUI.Label(new Rect(35f, nextLineY, 760f, 20f), "Molette : ajuste TopY | Shift + Molette : ajuste BaseY | Clic gauche : confirmer | Clic droit : annuler", _hintStyle!);
                nextLineY += 24f;
            }
            else
            {
                GUI.Label(new Rect(35f, nextLineY, 760f, 20f), "Clic gauche : ajoute un point | Clique près du premier point pour fermer | Clic droit : retire le dernier point", _hintStyle!);
                nextLineY += 24f;
            }
        }

        if (state.SelectedCategory == RegistryCategory.Diagnostics)
        {
            GUI.Label(new Rect(35f, nextLineY, 820f, 20f), "Diagnostics : inspection IA, édition anchor craft, simulation temps et flush registre.", _hintStyle!);
            nextLineY += 24f;
        }

        if (isCraftAnchorEditorActive)
        {
            GUI.Label(new Rect(35f, nextLineY, 820f, 20f), craftAnchorEditorStatus, _textStyle!);
            nextLineY += 24f;
            GUI.Label(new Rect(35f, nextLineY, 820f, 20f), craftAnchorEditorControls, _hintStyle!);
            nextLineY += 24f;
        }

        GUI.Label(new Rect(35f, nextLineY, 760f, 20f), "Astuce : avec 'Force assign', vise d'abord un PNJ enregistré, puis vise un slot, un siège, un lit ou un poste d'artisanat.", _hintStyle!);
        nextLineY += 24f;
        GUI.Label(new Rect(35f, nextLineY, 820f, 20f), $"{toggleKey} : mode | {nextCategoryKey} : catégorie | {nextActionKey} : action | Clic gauche : créer/éditer | Clic droit : supprimer/annuler auteur", _hintStyle!);
    }

    private void EnsureStyles()
    {
        if (_titleStyle != null) return;
        _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
        _textStyle = new GUIStyle(GUI.skin.label) { fontSize = 14 };
        _hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Italic };
    }

    private static string FormatCategory(RegistryCategory category) => category switch
    {
        RegistryCategory.Zones => "Zones",
        RegistryCategory.Slots => "Slots",
        RegistryCategory.Residents => "Residents",
        RegistryCategory.Diagnostics => "Diagnostics",
        _ => category.ToString()
    };

    private static string FormatAction(RegistryActionType action) => action switch
    {
        RegistryActionType.None => "Aucune",
        RegistryActionType.CreateTavernZone => "Créer zone : Taverne",
        RegistryActionType.CreateBedroomZone => "Créer zone : Chambre",
        RegistryActionType.CreateNavigationWaypoint => "Créer waypoint de navigation",
        RegistryActionType.ConnectNavigationWaypoints => "Connecter deux waypoints",
        RegistryActionType.DeleteNavigationWaypoint => "Supprimer waypoint de navigation",
        RegistryActionType.DeleteZone => "Supprimer zone",
        RegistryActionType.CreateInnkeeperSlot => "Créer slot : Aubergiste",
        RegistryActionType.DesignateSeatFurniture => "Désigner un siège dans le monde",
        RegistryActionType.DesignateBedFurniture => "Désigner un lit dans le monde",
        RegistryActionType.DesignateCraftStationFurniture => "Désigner un poste d'artisanat dans le monde",
        RegistryActionType.DeleteSlot => "Supprimer slot : Aubergiste",
        RegistryActionType.DeleteDesignatedSeat => "Supprimer siège désigné",
        RegistryActionType.DeleteDesignatedBed => "Supprimer lit désigné",
        RegistryActionType.DeleteDesignatedCraftStation => "Supprimer poste d'artisanat désigné",
        RegistryActionType.RegisterNpc => "Enregistrer PNJ visé",
        RegistryActionType.AssignInnkeeperRole => "Assigner rôle : Aubergiste",
        RegistryActionType.AssignSeat => "Assigner un siège désigné au PNJ visé",
        RegistryActionType.AssignBed => "Assigner un lit désigné au PNJ visé",
        RegistryActionType.ClearTargetInnkeeperSlotAssignment => "Effacer l'assignation du slot aubergiste visé",
        RegistryActionType.ClearTargetSeatAssignment => "Effacer l'assignation du siège visé",
        RegistryActionType.ClearTargetBedAssignment => "Effacer l'assignation du lit visé",
        RegistryActionType.ForceAssignResident => "Force assign : PNJ visé -> anchor visé",
        RegistryActionType.DespawnTargetResident => "Despawn PNJ visé",
        RegistryActionType.RespawnAssignedResident => "Respawn résident assigné à l'anchor visé",
        RegistryActionType.SpawnTestViking => "Faire apparaître PNJ test",
        RegistryActionType.InspectTargetNpcAi => "Inspecter IA du PNJ visé",
        RegistryActionType.EditTargetCraftStationAnchor => "Diagnostic : éditer anchor du poste d'artisanat visé",
        RegistryActionType.SimulateNoon => "Diagnostic : simuler midi",
        RegistryActionType.SimulateNight => "Diagnostic : simuler 22h00",
        RegistryActionType.ClearTimeSimulation => "Diagnostic : arrêter la simulation temps",
        RegistryActionType.FlushRegistryState => "Diagnostic : flush mémoire registre",
        _ => action.ToString()
    };

    private static string FormatPendingLink(int? waypointId) => waypointId.HasValue ? $"Waypoint #{waypointId.Value}" : "Aucun";

    private static string FormatPendingResidentForceAssign(RegistryToolState state) =>
        state.PendingResidentForceAssignId.HasValue ? $"#{state.PendingResidentForceAssignId.Value} '{state.PendingResidentForceAssignName}'" : "Aucun";

    private static string FormatZoneAuthoringPhase(ZoneAuthoringPhase phase) => phase switch
    {
        ZoneAuthoringPhase.Footprint => "Contour",
        ZoneAuthoringPhase.Height => "Hauteur",
        _ => "Aucune"
    };
}
