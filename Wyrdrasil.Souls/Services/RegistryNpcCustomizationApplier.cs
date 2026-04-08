using System;
using BepInEx.Logging;
using UnityEngine;
using Wyrdrasil.Registry.Tool;
using Wyrdrasil.Souls.Tool;
using Wyrdrasil.Souls.Components;

namespace Wyrdrasil.Souls.Services;


/// <summary>
/// Applies an already resolved NPC identity to the spawned runtime actor.
/// The initial application happens before activation, then a dedicated bootstrap component
/// reapplies the same state a few frames later once Valheim has finished its own setup.
/// </summary>
public sealed class RegistryNpcCustomizationApplier
{
    private readonly ManualLogSource _log;

    public RegistryNpcCustomizationApplier(ManualLogSource log)
    {
        _log = log;
    }

    public void Apply(GameObject runtimeObject, VikingIdentityData identity)
    {
        if (runtimeObject == null)
        {
            throw new ArgumentNullException(nameof(runtimeObject));
        }

        if (identity == null)
        {
            throw new ArgumentNullException(nameof(identity));
        }

        var visEquipment = runtimeObject.GetComponent<VisEquipment>();
        if (visEquipment == null)
        {
            _log.LogWarning("Customization skipped: spawned NPC has no VisEquipment component.");
            EnsureIdentityComponent(runtimeObject, identity);
            return;
        }

        EnsureIdentityComponent(runtimeObject, identity);
        EnsureBootstrapComponent(runtimeObject);
        var report = RegistryNpcVisualStateWriter.Apply(runtimeObject, identity, _log);
        _log.LogInfo($"[VisualApply] {runtimeObject.name} -> {report.ToSummary()}");
    }

    private static void EnsureIdentityComponent(GameObject gameObject, VikingIdentityData identity)
    {
        var identityComponent = gameObject.GetComponent<WyrdrasilVikingIdentityComponent>();
        if (identityComponent == null)
        {
            identityComponent = gameObject.AddComponent<WyrdrasilVikingIdentityComponent>();
        }

        identityComponent.SetIdentity(identity);
    }

    private static void EnsureBootstrapComponent(GameObject gameObject)
    {
        if (!gameObject.TryGetComponent<WyrdrasilVikingVisualBootstrap>(out _))
        {
            gameObject.AddComponent<WyrdrasilVikingVisualBootstrap>();
        }
    }
}
