using UnityEngine;
using Wyrdrasil.Registry.Diagnostics;

namespace Wyrdrasil.Registry.Components;

public sealed class WyrdrasilPlayerCraftDebugMonitor : MonoBehaviour
{
    private Animator? _animator;
    private int _lastFullPathHash = int.MinValue;
    private int _lastShortNameHash = int.MinValue;
    private bool _lastAttached;
    private int _traceFramesRemaining;
    private int _traceTickCounter;
    private string _traceLabel = string.Empty;

    public static WyrdrasilPlayerCraftDebugMonitor? EnsureAttached(Player? player)
    {
        if (player == null)
        {
            return null;
        }

        var existing = player.gameObject.GetComponent<WyrdrasilPlayerCraftDebugMonitor>();
        if (existing != null)
        {
            return existing;
        }

        return player.gameObject.AddComponent<WyrdrasilPlayerCraftDebugMonitor>();
    }

    public void BeginTrace(string label, int frames)
    {
        _traceLabel = label;
        _traceFramesRemaining = Mathf.Max(_traceFramesRemaining, frames);
        _traceTickCounter = 0;

        if (TryGetLocalPlayer(out var player))
        {
            WyrdrasilCraftDebug.LogAnimatorSnapshot(player, $"{label}.TraceStart", detailed: true);
        }
    }

    private void Awake()
    {
        _animator = GetComponentInChildren<Animator>(true);
        _lastAttached = TryGetLocalPlayer(out var player) && player.IsAttached();
        WyrdrasilCraftDebug.Log(gameObject, "Attached local player craft debug monitor.");
        WyrdrasilCraftDebug.LogAnimatorSnapshot(player, "MonitorAwake", detailed: true);
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
                WyrdrasilCraftDebug.LogAnimatorSnapshot(player, "AnimatorStateChanged", detailed: true);
                _lastFullPathHash = state.fullPathHash;
                _lastShortNameHash = state.shortNameHash;
            }
        }

        if (_traceFramesRemaining > 0)
        {
            _traceFramesRemaining--;
            _traceTickCounter++;
            if (_traceTickCounter % 5 == 0)
            {
                WyrdrasilCraftDebug.LogAnimatorSnapshot(player, $"{_traceLabel}.TraceTick{_traceTickCounter}", detailed: true);
            }
        }

        var attached = player.IsAttached();
        if (attached != _lastAttached)
        {
            _lastAttached = attached;
            WyrdrasilCraftDebug.Log(player, $"Player attached state changed -> {attached}");
            WyrdrasilCraftDebug.LogAnimatorSnapshot(player, "AttachedStateChanged", detailed: true);
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
