using BepInEx.Logging;
using UnityEngine;
using Wyrdrasil.Souls.Services;

namespace Wyrdrasil.Souls.Components;


public sealed class WyrdrasilVikingVisualBootstrap : MonoBehaviour
{
    private static ManualLogSource? _log;

    public static void ConfigureLogger(ManualLogSource log)
    {
        _log = log;
    }

    private const int FramesBeforeApply = 2;

    private int _framesRemaining = FramesBeforeApply;
    private bool _applied;

    private void LateUpdate()
    {
        var identity = GetComponent<WyrdrasilVikingIdentityComponent>()?.Identity;
        if (identity == null)
        {
            enabled = false;
            return;
        }

        if (_applied)
        {
            enabled = false;
            return;
        }

        if (_framesRemaining > 0)
        {
            _framesRemaining--;
            return;
        }

        RegistryNpcVisualStateWriter.ApplyRuntimeRefresh(gameObject, identity, _log);
        _applied = true;
    }
}