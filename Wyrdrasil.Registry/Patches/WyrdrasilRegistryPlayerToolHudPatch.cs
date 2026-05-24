using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine;
using Wyrdrasil.Registry.PlayerTool;

namespace Wyrdrasil.Registry.Patches;

[HarmonyPatch(typeof(Hud), "UpdateBuild")]
internal static class WyrdrasilRegistryPlayerToolHudPatch
{
    private static void Postfix(Hud __instance)
    {
        if (__instance == null)
        {
            return;
        }

        if (!RegistryPlayerToolHudTextService.IsRegistryToolBuildHudActive())
        {
            RegistryPlayerToolHudTextService.RestoreOriginalTexts();
            return;
        }

        RegistryPlayerToolHudTextService.ApplyRegistryTexts(__instance);
    }
}

internal static class RegistryPlayerToolHudTextService
{
    private sealed class TextBinding
    {
        public Component Component { get; }
        public PropertyInfo TextProperty { get; }
        public string OriginalText { get; }

        public TextBinding(Component component, PropertyInfo textProperty, string originalText)
        {
            Component = component;
            TextProperty = textProperty;
            OriginalText = originalText;
        }
    }

    private static readonly Dictionary<int, TextBinding> RewrittenTextsByInstanceId = new();

    private static readonly Dictionary<string, string> HintReplacements = new(StringComparer.OrdinalIgnoreCase)
    {
        ["$hud_place"] = "Valider",
        ["$hud_close"] = "Fermer",
        ["$hud_remove"] = "Annuler",
        ["$hud_buildmenu"] = "Menu du Registre",
        ["$hud_toggle_snap"] = "Options du Registre",
        ["$hud_cycle_snap_points"] = "Faire défiler les cibles",
        ["$hud_copy"] = "Copier / lier",
        ["$hud_rotate"] = "Ajuster",

        ["Placer"] = "Valider",
        ["Fermer le menu"] = "Fermer",
        ["Retirer"] = "Annuler",
        ["Menu de construction"] = "Menu du Registre",
        ["Activer/Désactiver snapping / options"] = "Options du Registre",
        ["Faire défiler les points d'ancrage"] = "Faire défiler les cibles",
        ["Copier"] = "Copier / lier",
        ["Pivoter"] = "Ajuster",

        ["Place"] = "Confirm",
        ["Close menu"] = "Close",
        ["Remove"] = "Cancel",
        ["Build menu"] = "Registry menu",
        ["Toggle snapping / options"] = "Registry options",
        ["Cycle snap points"] = "Cycle targets",
        ["Copy"] = "Copy / link",
        ["Rotate"] = "Adjust"
    };

    public static bool IsRegistryToolBuildHudActive()
    {
        var localPlayer = Player.m_localPlayer;
        if (localPlayer == null)
        {
            return false;
        }

        var buildPieces = GetPlayerBuildPieces(localPlayer);
        return buildPieces != null && string.Equals(
            buildPieces.name,
            RegistryPlayerToolConstants.PieceTableName,
            StringComparison.OrdinalIgnoreCase);
    }

    public static void ApplyRegistryTexts(Hud hud)
    {
        foreach (var component in hud.GetComponentsInChildren<Component>(true))
        {
            if (component == null)
            {
                continue;
            }

            var textProperty = FindWritableTextProperty(component);
            if (textProperty == null)
            {
                continue;
            }

            if (textProperty.GetValue(component, null) is not string currentText)
            {
                continue;
            }

            if (!TryGetReplacement(currentText, out var replacement))
            {
                continue;
            }

            var instanceId = component.GetInstanceID();
            if (!RewrittenTextsByInstanceId.ContainsKey(instanceId))
            {
                RewrittenTextsByInstanceId[instanceId] = new TextBinding(component, textProperty, currentText);
            }

            if (!string.Equals(currentText, replacement, StringComparison.Ordinal))
            {
                textProperty.SetValue(component, replacement, null);
            }
        }
    }

    public static void RestoreOriginalTexts()
    {
        if (RewrittenTextsByInstanceId.Count == 0)
        {
            return;
        }

        var staleKeys = new List<int>();
        foreach (var pair in RewrittenTextsByInstanceId)
        {
            var binding = pair.Value;
            if (binding.Component == null)
            {
                staleKeys.Add(pair.Key);
                continue;
            }

            if (binding.TextProperty.GetValue(binding.Component, null) is string currentText &&
                IsRegistryReplacement(currentText))
            {
                binding.TextProperty.SetValue(binding.Component, binding.OriginalText, null);
            }

            staleKeys.Add(pair.Key);
        }

        foreach (var key in staleKeys)
        {
            RewrittenTextsByInstanceId.Remove(key);
        }
    }

    private static PieceTable? GetPlayerBuildPieces(Player player)
    {
        for (var type = player.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(
                "m_buildPieces",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (field?.GetValue(player) is PieceTable pieceTable)
            {
                return pieceTable;
            }
        }

        return null;
    }

    private static PropertyInfo? FindWritableTextProperty(Component component)
    {
        var type = component.GetType();
        var textProperty = type.GetProperty(
            "text",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (textProperty == null || textProperty.PropertyType != typeof(string) || !textProperty.CanRead || !textProperty.CanWrite)
        {
            return null;
        }

        return textProperty;
    }

    private static bool TryGetReplacement(string text, out string replacement)
    {
        return HintReplacements.TryGetValue(Normalize(text), out replacement!);
    }

    private static bool IsRegistryReplacement(string text)
    {
        var normalized = Normalize(text);
        foreach (var replacement in HintReplacements.Values)
        {
            if (string.Equals(normalized, Normalize(replacement), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string Normalize(string text)
    {
        return Regex.Replace(text.Replace('\u00a0', ' '), @"\s+", " ").Trim();
    }
}
