using System;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;

namespace Wyrdrasil.Registry.PlayerTool.Localization;

public sealed class RegistryPlayerToolLocalizationService
{
    private readonly ManualLogSource _log;
    private bool _registered;
    private bool _unavailableWarningLogged;

    public RegistryPlayerToolLocalizationService(ManualLogSource log)
    {
        _log = log;
    }

    public void EnsureRegistered()
    {
        if (_registered)
        {
            return;
        }

        if (!TryAddWord("item_jotunkonung_ring", RegistryPlayerToolConstants.LocalizedDisplayName) ||
            !TryAddWord("item_jotunkonung_ring_desc", RegistryPlayerToolConstants.LocalizedDescription))
        {
            if (!_unavailableWarningLogged)
            {
                _log.LogWarning("Valheim localization service is not available yet; Jotunkonung Ring localization will be retried later.");
                _unavailableWarningLogged = true;
            }

            return;
        }

        _registered = true;
        _log.LogInfo("Registered Jotunkonung Ring localization entries.");
    }

    private bool TryAddWord(string key, string value)
    {
        try
        {
            var localizationType = FindLocalizationType();
            if (localizationType == null)
            {
                return false;
            }

            var addWordMethod = localizationType.GetMethod(
                "AddWord",
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(string), typeof(string) },
                null);

            if (addWordMethod == null)
            {
                return false;
            }

            object? localizationInstance = null;
            if (!addWordMethod.IsStatic)
            {
                localizationInstance = ResolveLocalizationInstance(localizationType);
                if (localizationInstance == null)
                {
                    return false;
                }
            }

            addWordMethod.Invoke(localizationInstance, new object[] { key, value });
            return true;
        }
        catch (Exception exception)
        {
            _log.LogWarning($"Failed to register localization word '{key}': {exception.Message}");
            return false;
        }
    }

    private static Type? FindLocalizationType()
    {
        return AppDomain.CurrentDomain
            .GetAssemblies()
            .Select(assembly => assembly.GetType("Localization", false))
            .FirstOrDefault(type => type != null);
    }

    private static object? ResolveLocalizationInstance(Type localizationType)
    {
        var instanceField = localizationType.GetField(
            "instance",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        if (instanceField != null)
        {
            return instanceField.GetValue(null);
        }

        var instanceProperty = localizationType.GetProperty(
            "instance",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        return instanceProperty != null ? instanceProperty.GetValue(null, null) : null;
    }
}
