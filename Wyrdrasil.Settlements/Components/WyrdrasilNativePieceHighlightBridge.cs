using System;
using System.Linq;
using UnityEngine;
using Wyrdrasil.Core.Tool;

namespace Wyrdrasil.Settlements.Components;

/// <summary>
/// Bridges Wyrdrasil furniture highlights to Valheim's native build-piece highlight material pipeline.
///
/// Important distinction:
/// - calling WearNTear.Highlight() alone is not sufficient for persistent inspection highlights in
///   the current Valheim build;
/// - Valheim's visible build-piece highlight lifecycle is MaterialMan.SetValue(...) followed by the
///   native WearNTear ResetHighlight invoke window.
///
/// This component therefore avoids Wyrdrasil mesh/ring overlays for native pieces and delegates the
/// visible effect to Valheim's MaterialMan + WearNTear reset lifecycle instead.
/// </summary>
public sealed class WyrdrasilNativePieceHighlightBridge : MonoBehaviour
{
    private const string LogPrefix = "[Wyrdrasil.Registry][NativePieceHighlight]";

    private GameObject? _targetRoot;
    private WearNTear? _wearNTear;
    private Piece? _piece;
    private GameObject? _highlightRoot;
    private bool _isVisible;
    private bool _diagnosticsLogged;

    public bool CanUseNativeHighlight => _wearNTear != null || _piece != null;

    public void Initialize(GameObject targetRoot)
    {
        if (_targetRoot == targetRoot)
        {
            return;
        }

        ResetCurrentHighlight();
        _targetRoot = targetRoot;
        _wearNTear = ResolveWearNTear(targetRoot);
        _piece = ResolvePiece(targetRoot);
        _highlightRoot = ResolveHighlightRoot(targetRoot, _wearNTear, _piece);
        _isVisible = false;
        _diagnosticsLogged = false;
    }

    /// <returns>
    /// True when the target is a native Valheim piece and the native material-highlight lifecycle
    /// handled the request. False means callers may use their non-piece fallback visuals.
    /// </returns>
    public bool SetVisible(bool visible, Color color, bool emphasized)
    {
        if (_targetRoot == null || !CanUseNativeHighlight)
        {
            return false;
        }

        if (!visible)
        {
            ResetCurrentHighlight();
            return true;
        }

        _isVisible = true;

        var highlightColor = emphasized
            ? color
            : Color.Lerp(color, WyrdrasilVisualizationPalette.AssignedPurple, 0.5f);
        highlightColor.a = 1f;

        var target = _highlightRoot ?? _wearNTear?.gameObject ?? _piece?.gameObject ?? _targetRoot;
        var emissionColor = highlightColor * (emphasized ? 0.95f : 0.65f);
        emissionColor.a = 1f;

        var applied = WyrdrasilNativePieceHighlightState.ApplyNativeMaterialColor(
            target,
            highlightColor,
            emissionColor,
            out var appliedColor,
            out var appliedEmission,
            out var materialStatus);

        if (applied)
        {
            WyrdrasilNativePieceHighlightState.RefreshNativeResetWindow(_wearNTear);
            LogDiagnosticsOnce(
                backend: "MaterialMan.SetValue + WearNTear.Invoke(ResetHighlight)",
                appliedNativeWearNTear: _wearNTear != null,
                appliedMaterialColor: appliedColor,
                appliedMaterialEmission: appliedEmission,
                materialStatus: materialStatus);
            return true;
        }

        LogDiagnosticsOnce(
            backend: "native MaterialMan unavailable",
            appliedNativeWearNTear: false,
            appliedMaterialColor: appliedColor,
            appliedMaterialEmission: appliedEmission,
            materialStatus: materialStatus);

        return false;
    }

    private void ResetCurrentHighlight()
    {
        if (!_isVisible)
        {
            return;
        }

        _isVisible = false;
        WyrdrasilNativePieceHighlightState.RequestNativeReset(_wearNTear);
    }

    private void LogDiagnosticsOnce(
        string backend,
        bool appliedNativeWearNTear,
        bool appliedMaterialColor,
        bool appliedMaterialEmission,
        string materialStatus)
    {
        if (_diagnosticsLogged)
        {
            return;
        }

        _diagnosticsLogged = true;

        var targetName = _targetRoot != null ? _targetRoot.name : "<none>";
        var highlightName = _highlightRoot != null ? _highlightRoot.name : "<none>";
        var wearName = _wearNTear != null ? _wearNTear.gameObject.name : "<none>";
        var pieceName = _piece != null ? _piece.gameObject.name : "<none>";
        var rendererCount = _targetRoot != null ? _targetRoot.GetComponentsInChildren<Renderer>(true).Count(IsGameRenderer) : 0;
        var meshFilterCount = _targetRoot != null ? _targetRoot.GetComponentsInChildren<MeshFilter>(true).Length : 0;

        Debug.Log(
            $"{LogPrefix} target='{targetName}' highlightRoot='{highlightName}' piece='{pieceName}' " +
            $"wearNTear='{wearName}' renderers={rendererCount} meshFilters={meshFilterCount} " +
            $"backend='{backend}' appliedNativeWearNTear={appliedNativeWearNTear} " +
            $"appliedMaterialColor={appliedMaterialColor} appliedMaterialEmission={appliedMaterialEmission} " +
            $"materialMan={materialStatus}");
    }

    private static WearNTear? ResolveWearNTear(GameObject root)
    {
        return root.GetComponent<WearNTear>()
               ?? root.GetComponentInParent<WearNTear>()
               ?? root.GetComponentInChildren<WearNTear>(true);
    }

    private static Piece? ResolvePiece(GameObject root)
    {
        return root.GetComponent<Piece>()
               ?? root.GetComponentInParent<Piece>()
               ?? root.GetComponentInChildren<Piece>(true);
    }

    private static GameObject ResolveHighlightRoot(GameObject root, WearNTear? wearNTear, Piece? piece)
    {
        if (wearNTear != null)
        {
            return wearNTear.gameObject;
        }

        if (piece != null)
        {
            return piece.gameObject;
        }

        return root;
    }

    private static bool IsGameRenderer(Renderer? renderer)
    {
        if (renderer == null || renderer is LineRenderer)
        {
            return false;
        }

        var current = renderer.transform;
        while (current != null)
        {
            if (current.name.StartsWith("Wyrdrasil_", StringComparison.Ordinal) ||
                current.GetComponentInParent<WyrdrasilFurnitureGlowOverlay>() != null)
            {
                return false;
            }

            current = current.parent;
        }

        return true;
    }

    private void OnDisable()
    {
        ResetCurrentHighlight();
    }

    private void OnDestroy()
    {
        ResetCurrentHighlight();
    }
}
