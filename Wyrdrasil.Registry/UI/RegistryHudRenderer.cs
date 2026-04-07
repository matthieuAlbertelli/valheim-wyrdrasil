using UnityEngine;
using Wyrdrasil.Registry.Tool;

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
        int residentCount,
        PendingZoneAuthoringSnapshot? pendingZoneAuthoring)
    {
        if (!state.IsRegistryModeEnabled)
        {
            return;
        }

        EnsureStyles();

        var panelHeight = pendingZoneAuthoring == null ? 338f : 434f;
        var panelRect = new Rect(20f, 180f, 820f, panelHeight);

        GUI.Box(panelRect, GUIContent.none);
        GUI.Label(new Rect(35f, 190f, 320f, 24f), "Registre des Âmes", _titleStyle!);
        GUI.Label(new Rect(35f, 218f, 360f, 20f), "Mode Registre actif", _textStyle!);
        GUI.Label(new Rect(35f, 242f, 680f, 20f), $"Catégorie : {FormatCategory(state.SelectedCategory)}", _textStyle!);
        GUI.Label(new Rect(35f, 266f, 760f, 20f), $"Action : {FormatAction(state.SelectedAction)}", _textStyle!);
        GUI.Label(new Rect(35f, 290f, 420f, 20f), $"Zones : {zoneCount}", _textStyle!);
        GUI.Label(new Rect(35f, 314f, 420f, 20f), $"Waypoints : {waypointCount}", _textStyle!);
        GUI.Label(new Rect(35f, 338f, 540f, 20f), $"Départ de lien sélectionné : {FormatPendingLink(pendingLinkStartWaypointId)}", _textStyle!);
        GUI.Label(new Rect(35f, 362f, 420f, 20f), $"Slots aubergiste : {slotCount}", _textStyle!);
        GUI.Label(new Rect(35f, 386f, 420f, 20f), $"Sièges désignés : {seatCount}", _textStyle!);
        GUI.Label(new Rect(35f, 410f, 420f, 20f), $"PNJ enregistrés : {residentCount}", _textStyle!);
        GUI.Label(new Rect(35f, 434f, 760f, 20f), $"Résident sélectionné pour force assign : {FormatPendingResidentForceAssign(state)}", _textStyle!);

        var nextLineY = 458f;
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

        GUI.Label(new Rect(35f, nextLineY, 760f, 20f), "Astuce : avec 'Force assign', vise d'abord un PNJ enregistré, puis vise un slot ou un siège.", _hintStyle!);
        nextLineY += 24f;
        GUI.Label(
            new Rect(35f, nextLineY, 760f, 20f),
            $"{toggleKey} : mode | {nextCategoryKey} : catégorie | {nextActionKey} : action | Clic gauche : créer/éditer | Clic droit : supprimer/annuler auteur",
            _hintStyle!);
    }

    private void EnsureStyles()
    {
        if (_titleStyle is not null)
        {
            return;
        }

        _titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold
        };

        _textStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14
        };

        _hintStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Italic
        };
    }

    private static string FormatCategory(RegistryCategory category)
    {
        return category switch
        {
            RegistryCategory.Zones => "Zones",
            RegistryCategory.Slots => "Slots",
            RegistryCategory.Residents => "Residents",
            RegistryCategory.Diagnostics => "Diagnostics",
            _ => category.ToString()
        };
    }

    private static string FormatAction(RegistryActionType action)
    {
        return action switch
        {
            RegistryActionType.None => "Aucune",
            RegistryActionType.CreateTavernZone => "Créer zone : Taverne",
            RegistryActionType.CreateNavigationWaypoint => "Créer waypoint de navigation",
            RegistryActionType.ConnectNavigationWaypoints => "Connecter deux waypoints",
            RegistryActionType.DeleteNavigationWaypoint => "Supprimer waypoint de navigation",
            RegistryActionType.DeleteZone => "Supprimer zone",
            RegistryActionType.CreateInnkeeperSlot => "Créer slot : Aubergiste",
            RegistryActionType.DesignateSeatFurniture => "Désigner un siège dans le monde",
            RegistryActionType.DeleteSlot => "Supprimer slot : Aubergiste",
            RegistryActionType.DeleteDesignatedSeat => "Supprimer siège désigné",
            RegistryActionType.RegisterNpc => "Enregistrer PNJ visé",
            RegistryActionType.AssignInnkeeperRole => "Assigner rôle : Aubergiste",
            RegistryActionType.AssignSeat => "Assigner un siège désigné au PNJ visé",
            RegistryActionType.ClearTargetInnkeeperSlotAssignment => "Effacer l'assignation du slot aubergiste visé",
            RegistryActionType.ClearTargetSeatAssignment => "Effacer l'assignation du siège visé",
            RegistryActionType.ForceAssignResident => "Force assign : PNJ visé -> slot/siège visé",
            RegistryActionType.DespawnTargetResident => "Despawn PNJ visé",
            RegistryActionType.RespawnAssignedResident => "Respawn résident assigné au slot/siège visé",
            RegistryActionType.SpawnTestViking => "Faire apparaître PNJ test",
            RegistryActionType.InspectTargetNpcAi => "Inspecter IA du PNJ visé",
            _ => action.ToString()
        };
    }

    private static string FormatPendingLink(int? waypointId)
    {
        return waypointId.HasValue ? $"Waypoint #{waypointId.Value}" : "Aucun";
    }

    private static string FormatPendingResidentForceAssign(RegistryToolState state)
    {
        return state.PendingResidentForceAssignId.HasValue
            ? $"#{state.PendingResidentForceAssignId.Value} '{state.PendingResidentForceAssignName}'"
            : "Aucun";
    }

    private static string FormatZoneAuthoringPhase(ZoneAuthoringPhase phase)
    {
        return phase switch
        {
            ZoneAuthoringPhase.Footprint => "Contour",
            ZoneAuthoringPhase.Height => "Hauteur",
            _ => "Aucune"
        };
    }
}
