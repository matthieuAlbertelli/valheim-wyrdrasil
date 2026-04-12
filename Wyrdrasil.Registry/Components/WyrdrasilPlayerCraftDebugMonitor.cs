using UnityEngine;
using Wyrdrasil.Registry.Diagnostics;

namespace Wyrdrasil.Registry.Components;

public sealed class WyrdrasilPlayerCraftDebugMonitor : MonoBehaviour
{
    private Animator? _animator;
    private int _lastFullPathHash = int.MinValue;
    private int _lastShortNameHash = int.MinValue;
    private bool _lastAttached;

    public static void EnsureAttached(Player? player)
    {
        if (player == null || player.gameObject.GetComponent<WyrdrasilPlayerCraftDebugMonitor>() != null)
        {
            return;
        }

        player.gameObject.AddComponent<WyrdrasilPlayerCraftDebugMonitor>();
    }

    private void Awake()
    {
        _animator = GetComponentInChildren<Animator>(true);
        _lastAttached = TryGetLocalPlayer(out var player) && player.IsAttached();
        WyrdrasilCraftDebug.Log(gameObject, "Attached local player craft debug monitor.");
        WyrdrasilCraftDebug.LogAnimatorSnapshot(player, "MonitorAwake");
        CaptureCurrentHashes();
    }

    private void Update()
    {
        if (!TryGetLocalPlayer(out var player) || player.gameObject != gameObject)
        {
            Destroy(this);
            return;
        }

        if (_animator == null)
        {
            _animator = GetComponentInChildren<Animator>(true);
        }

        if (_animator != null)
        {
            var state = _animator.GetCurrentAnimatorStateInfo(0);
            if (state.fullPathHash != _lastFullPathHash || state.shortNameHash != _lastShortNameHash)
            {
                WyrdrasilCraftDebug.LogAnimatorSnapshot(player, "AnimatorStateChanged");
                _lastFullPathHash = state.fullPathHash;
                _lastShortNameHash = state.shortNameHash;
            }
        }

        var attached = player.IsAttached();
        if (attached != _lastAttached)
        {
            _lastAttached = attached;
            WyrdrasilCraftDebug.Log(player, $"Player attached state changed -> {attached}");
            WyrdrasilCraftDebug.LogAnimatorSnapshot(player, "AttachedStateChanged");
        }
    }

    private void CaptureCurrentHashes()
    {
        if (_animator == null)
        {
            return;
        }

        var state = _animator.GetCurrentAnimatorStateInfo(0);
        _lastFullPathHash = state.fullPathHash;
        _lastShortNameHash = state.shortNameHash;
    }

    private static bool TryGetLocalPlayer(out Player player)
    {
        player = Player.m_localPlayer;
        return player != null;
    }
}
