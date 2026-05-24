using System;
using System.Reflection;
using BepInEx.Logging;
using UnityEngine;
using Wyrdrasil.Registry.Components;
using Wyrdrasil.Settlements.Components;

namespace Wyrdrasil.Registry.PlayerTool;

public sealed class RegistryPlayerToolInspectionService
{
    private const float MaxInspectDistance = 100f;

    private readonly ManualLogSource _log;

    public RegistryPlayerToolInspectionService(ManualLogSource log)
    {
        _log = log;
    }

    public void InspectCrosshairTarget()
    {
        var message = BuildInspectionMessage();
        _log.LogInfo($"Registry player inspect: {message}");
        ShowPlayerMessage(message);
    }

    private static string BuildInspectionMessage()
    {
        if (RegistryPlayerToolWorldTargeting.TryGetTargetCharacter(out var targetedCharacter))
        {
            var characterObject = targetedCharacter.gameObject;
            if (TryDescribeWyrdrasilTarget(characterObject, out var characterDescription))
            {
                return characterDescription;
            }

            return $"Registre : {CleanName(characterObject.name)} — viking non enregistré.";
        }

        if (!RegistryPlayerToolWorldTargeting.TryGetRegularRaycastTarget(out var hitInfo))
        {
            return "Registre : aucune cible.";
        }

        var targetObject = hitInfo.collider.gameObject;
        if (TryDescribeWyrdrasilTarget(targetObject, out var wyrdrasilDescription))
        {
            return wyrdrasilDescription;
        }

        var piece = targetObject.GetComponentInParent<Piece>();
        if (piece != null)
        {
            return $"Registre : {CleanName(piece.gameObject.name)} — pièce Valheim.";
        }

        return $"Registre : {CleanName(targetObject.name)}.";
    }

    private static bool TryDescribeWyrdrasilTarget(GameObject targetObject, out string description)
    {
        var npcMarker = targetObject.GetComponentInParent<WyrdrasilRegisteredNpcMarker>();
        if (npcMarker != null)
        {
            var displayName = string.IsNullOrWhiteSpace(npcMarker.DisplayName)
                ? $"Âme #{npcMarker.RegisteredNpcId}"
                : npcMarker.DisplayName;

            description = $"Âme : {displayName} — rôle {npcMarker.Role}.";
            return true;
        }

        var zoneMarker = targetObject.GetComponentInParent<WyrdrasilFunctionalZoneMarker>();
        if (zoneMarker != null)
        {
            description = $"Lieu : {zoneMarker.ZoneType} #{zoneMarker.ZoneId}.";
            return true;
        }

        var slotMarker = targetObject.GetComponentInParent<WyrdrasilZoneSlotMarker>();
        if (slotMarker != null)
        {
            description = $"Poste : {slotMarker.SlotType} #{slotMarker.SlotId} — lieu #{slotMarker.ZoneId}.";
            return true;
        }

        var bedMarker = targetObject.GetComponentInParent<WyrdrasilRegisteredBedMarker>();
        if (bedMarker != null)
        {
            description = $"Lit enregistré #{ReadPrivateIntField(bedMarker, "_bedId")}.";
            return true;
        }

        var seatMarker = targetObject.GetComponentInParent<WyrdrasilRegisteredSeatMarker>();
        if (seatMarker != null)
        {
            description = $"Siège enregistré #{ReadPrivateIntField(seatMarker, "_seatId")}.";
            return true;
        }

        var craftStationMarker = targetObject.GetComponentInParent<WyrdrasilRegisteredCraftStationMarker>();
        if (craftStationMarker != null)
        {
            description = $"Poste de travail enregistré #{ReadPrivateIntField(craftStationMarker, "_craftStationId")}.";
            return true;
        }

        var waypointMarker = targetObject.GetComponentInParent<WyrdrasilNavigationWaypointMarker>();
        if (waypointMarker != null)
        {
            description = $"Point de chemin #{waypointMarker.WaypointId}.";
            return true;
        }

        description = string.Empty;
        return false;
    }

    private static int ReadPrivateIntField(object target, string fieldName)
    {
        var field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (field?.GetValue(target) is int value)
        {
            return value;
        }

        return 0;
    }

    private static string CleanName(string name)
    {
        var cloneIndex = name.IndexOf("(Clone)", StringComparison.OrdinalIgnoreCase);
        if (cloneIndex >= 0)
        {
            name = name.Substring(0, cloneIndex);
        }

        return name.Trim();
    }

    private void ShowPlayerMessage(string message)
    {
        var player = Player.m_localPlayer;
        if (player == null)
        {
            return;
        }

        try
        {
            player.Message(MessageHud.MessageType.Center, message, 0, null);
        }
        catch (Exception exception)
        {
            _log.LogWarning($"Could not display registry inspect message in HUD: {exception.Message}");
        }
    }
}
