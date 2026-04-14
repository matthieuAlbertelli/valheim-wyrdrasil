using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;
using Wyrdrasil.Construction.Diagnostics;
using Wyrdrasil.Construction.Models;

namespace Wyrdrasil.Construction.Services;

public sealed class ConstructionPlacementPreviewService
{
    private sealed class PreviewPiece
    {
        public int PieceId { get; }
        public GameObject Root { get; }
        public List<Renderer> Renderers { get; }

        public PreviewPiece(int pieceId, GameObject root, List<Renderer> renderers)
        {
            PieceId = pieceId;
            Root = root;
            Renderers = renderers;
        }
    }

    private static readonly Color ValidColor = new Color(0.2f, 1f, 0.35f, 0.55f);
    private static readonly Color InvalidColor = new Color(1f, 0.25f, 0.25f, 0.55f);
    private const float RotationStepDegrees = 45f;

    private readonly BlueprintCatalogService _blueprintCatalogService;
    private readonly ConstructionPlacementService _constructionPlacementService;
    private readonly ConstructionProjectService _constructionProjectService;
    private readonly ConstructionDebugLogService _debugLogService;
    private readonly List<PreviewPiece> _previewPieces = new();

    private StructureBlueprintData? _activeBlueprint;
    private Vector3 _originPosition;
    private int _rotationStepIndex;
    private bool _isPlacementValid;
    private string _validationMessage = "No active construction preview.";

    public ConstructionPlacementPreviewService(
        BlueprintCatalogService blueprintCatalogService,
        ConstructionPlacementService constructionPlacementService,
        ConstructionProjectService constructionProjectService,
        ConstructionDebugLogService debugLogService)
    {
        _blueprintCatalogService = blueprintCatalogService;
        _constructionPlacementService = constructionPlacementService;
        _constructionProjectService = constructionProjectService;
        _debugLogService = debugLogService;
    }

    public bool IsPreviewActive => _activeBlueprint != null;
    public string ActiveBlueprintId => _activeBlueprint?.Id ?? string.Empty;
    public string StatusLabel => !IsPreviewActive
        ? "Construction preview inactive."
        : $"Construction preview: {ActiveBlueprintId} | Valid: {(_isPlacementValid ? "Yes" : "No")} | {_validationMessage}";
    public string ControlsLabel => "Construction preview: Molette = pivoter | Clic gauche = lancer le chantier | Clic droit = annuler";

    public bool TryBeginPreview(string blueprintId, Vector3 originPosition, Quaternion initialRotation, out string failureReason)
    {
        CancelPreview();

        if (!_blueprintCatalogService.TryGetBlueprint(blueprintId, out var blueprint))
        {
            failureReason = $"Unknown blueprint '{blueprintId}'.";
            return false;
        }

        _activeBlueprint = blueprint;
        _originPosition = originPosition;
        _rotationStepIndex = Mathf.RoundToInt(initialRotation.eulerAngles.y / RotationStepDegrees);

        if (!RefreshPreview(out failureReason))
        {
            CancelPreview();
            return false;
        }

        _debugLogService.Info("Preview", $"Started construction preview for blueprint '{blueprintId}'.");
        return true;
    }

    public void UpdatePreviewPosition(Vector3 originPosition)
    {
        if (!IsPreviewActive)
        {
            return;
        }

        _originPosition = originPosition;
        RefreshPreview(out _);
    }

    public void RotatePreview(float scrollDelta)
    {
        if (!IsPreviewActive || Mathf.Abs(scrollDelta) < 0.01f)
        {
            return;
        }

        _rotationStepIndex += scrollDelta > 0f ? 1 : -1;
        RefreshPreview(out _);
    }

    public bool TryConfirmPreview(out ConstructionProjectData project, out string failureReason)
    {
        project = new ConstructionProjectData();

        if (_activeBlueprint == null)
        {
            failureReason = "No construction preview is active.";
            return false;
        }

        if (!_isPlacementValid)
        {
            failureReason = string.IsNullOrWhiteSpace(_validationMessage)
                ? "The current construction preview is invalid."
                : _validationMessage;
            return false;
        }

        project = _constructionProjectService.CreateProject(
            _activeBlueprint,
            _originPosition,
            GetCurrentRotation());

        CancelPreview();
        failureReason = string.Empty;
        return true;
    }

    public void CancelPreview()
    {
        foreach (var previewPiece in _previewPieces)
        {
            if (previewPiece.Root != null)
            {
                Object.Destroy(previewPiece.Root);
            }
        }

        _previewPieces.Clear();
        _activeBlueprint = null;
        _originPosition = Vector3.zero;
        _rotationStepIndex = 0;
        _isPlacementValid = false;
        _validationMessage = "No active construction preview.";
    }

    private bool RefreshPreview(out string failureReason)
    {
        failureReason = string.Empty;

        if (_activeBlueprint == null)
        {
            failureReason = "No construction preview is active.";
            return false;
        }

        if (!_constructionPlacementService.TryResolveBlueprintPlacements(
                _activeBlueprint,
                _originPosition,
                GetCurrentRotation(),
                out var placements,
                out failureReason))
        {
            _isPlacementValid = false;
            _validationMessage = failureReason;
            ApplyPreviewColor(InvalidColor);
            return false;
        }

        EnsurePreviewObjects(placements);
        UpdatePreviewTransforms(placements);

        _isPlacementValid = EvaluateBlockingOverlaps(out _validationMessage);
        ApplyPreviewColor(_isPlacementValid ? ValidColor : InvalidColor);
        return true;
    }

    private void EnsurePreviewObjects(IReadOnlyList<ConstructionResolvedPiecePlacement> placements)
    {
        var mustRebuild = _previewPieces.Count != placements.Count;

        if (!mustRebuild)
        {
            for (var index = 0; index < placements.Count; index++)
            {
                if (_previewPieces[index].PieceId != placements[index].PieceId)
                {
                    mustRebuild = true;
                    break;
                }
            }
        }

        if (!mustRebuild)
        {
            return;
        }

        CancelPreviewObjectsOnly();

        foreach (var placement in placements)
        {
            var root = Object.Instantiate(placement.Prefab, placement.WorldPosition, placement.WorldRotation);
            root.name = $"{placement.Prefab.name}_ConstructionPreview";
            PrepareGhostHierarchy(root);

            var renderers = root.GetComponentsInChildren<Renderer>(true).ToList();
            _previewPieces.Add(new PreviewPiece(placement.PieceId, root, renderers));
        }
    }

    private void CancelPreviewObjectsOnly()
    {
        foreach (var previewPiece in _previewPieces)
        {
            if (previewPiece.Root != null)
            {
                Object.Destroy(previewPiece.Root);
            }
        }

        _previewPieces.Clear();
    }

    private void UpdatePreviewTransforms(IReadOnlyList<ConstructionResolvedPiecePlacement> placements)
    {
        for (var index = 0; index < placements.Count; index++)
        {
            var placement = placements[index];
            var previewPiece = _previewPieces[index];
            if (previewPiece.Root == null)
            {
                continue;
            }

            previewPiece.Root.transform.SetPositionAndRotation(placement.WorldPosition, placement.WorldRotation);
        }
    }

    private void PrepareGhostHierarchy(GameObject root)
    {
        SetLayerRecursively(root, LayerMask.NameToLayer("Ignore Raycast"));

        foreach (var collider in root.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = false;
        }

        foreach (var rigidbody in root.GetComponentsInChildren<Rigidbody>(true))
        {
            rigidbody.isKinematic = true;
            rigidbody.detectCollisions = false;
        }
    }

    private bool EvaluateBlockingOverlaps(out string validationMessage)
    {
        foreach (var previewPiece in _previewPieces)
        {
            foreach (var renderer in previewPiece.Renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                var bounds = renderer.bounds;
                if (bounds.size.sqrMagnitude <= 0.0001f)
                {
                    continue;
                }

                var colliders = Physics.OverlapBox(bounds.center, bounds.extents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                foreach (var collider in colliders)
                {
                    if (collider == null)
                    {
                        continue;
                    }

                    if (collider.transform.IsChildOf(previewPiece.Root.transform))
                    {
                        continue;
                    }

                    if (!IsBlockingCollider(collider))
                    {
                        continue;
                    }

                    validationMessage = $"Blocked by '{collider.name}'.";
                    return false;
                }
            }
        }

        validationMessage = "Placement is valid.";
        return true;
    }

    private static bool IsBlockingCollider(Collider collider)
    {
        if (collider.GetComponentInParent<Character>() != null)
        {
            return true;
        }

        if (collider.GetComponentInParent<Piece>() != null)
        {
            return true;
        }

        return false;
    }

    private void ApplyPreviewColor(Color color)
    {
        foreach (var previewPiece in _previewPieces)
        {
            foreach (var renderer in previewPiece.Renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                if (renderer is LineRenderer lineRenderer)
                {
                    lineRenderer.startColor = color;
                    lineRenderer.endColor = color;
                }

                var material = renderer.material;
                if (material != null && material.HasProperty("_Color"))
                {
                    material.color = color;
                }
            }
        }
    }

    private Quaternion GetCurrentRotation()
    {
        return Quaternion.Euler(0f, _rotationStepIndex * RotationStepDegrees, 0f);
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        if (layer < 0)
        {
            return;
        }

        root.layer = layer;
        foreach (Transform child in root.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }
}
