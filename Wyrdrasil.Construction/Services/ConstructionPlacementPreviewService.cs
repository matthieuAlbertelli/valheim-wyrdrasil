using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
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

    private static readonly Color ValidColor = new Color(0.2f, 1f, 0.35f, 0.18f);
    private static readonly Color InvalidColor = new Color(1f, 0.25f, 0.25f, 0.18f);
    private const float RotationStepDegrees = 45f;
    private const float VerticalOffsetStep = 0.5f;

    private readonly BlueprintCatalogService _blueprintCatalogService;
    private readonly ConstructionPlacementService _constructionPlacementService;
    private readonly ConstructionProjectService _constructionProjectService;
    private readonly ConstructionDebugLogService _debugLogService;
    private readonly List<PreviewPiece> _previewPieces = new();

    private StructureBlueprintData? _activeBlueprint;
    private Vector3 _anchorPosition;
    private float _verticalOffset;
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
        : $"Construction preview: {ActiveBlueprintId} | Valid: {(_isPlacementValid ? "Yes" : "No")} | Y Offset: {_verticalOffset:0.00} | {_validationMessage}";
    public string ControlsLabel => "Construction preview: Molette = pivoter | Shift + Molette = élever/enfoncer | Clic gauche = lancer le chantier | Clic droit = annuler";

    public bool TryBeginPreview(string blueprintId, Vector3 originPosition, Quaternion initialRotation, out string failureReason)
    {
        CancelPreview();

        if (!_blueprintCatalogService.TryGetBlueprint(blueprintId, out var blueprint))
        {
            failureReason = $"Unknown blueprint '{blueprintId}'.";
            return false;
        }

        _activeBlueprint = blueprint;
        _anchorPosition = originPosition;
        _verticalOffset = 0f;
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

        _anchorPosition = originPosition;
        RefreshPreview(out _);
    }

    public void AdjustPreviewHeight(float scrollDelta)
    {
        if (!IsPreviewActive || Mathf.Abs(scrollDelta) < 0.01f)
        {
            return;
        }

        _verticalOffset += scrollDelta > 0f ? VerticalOffsetStep : -VerticalOffsetStep;
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
            GetCurrentOriginPosition(),
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
        _anchorPosition = Vector3.zero;
        _verticalOffset = 0f;
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
                GetCurrentOriginPosition(),
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

                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                if (renderer is LineRenderer lineRenderer)
                {
                    lineRenderer.startColor = color;
                    lineRenderer.endColor = color;
                }

                var material = renderer.material;
                if (material == null)
                {
                    continue;
                }

                ConfigureGhostMaterial(material, color);
            }
        }
    }

    private static void ConfigureGhostMaterial(Material material, Color color)
    {
        if (material.HasProperty("_Color"))
        {
            material.color = color;
        }

        if (material.HasProperty("_EmissionColor"))
        {
            material.SetColor("_EmissionColor", new Color(color.r * 0.15f, color.g * 0.15f, color.b * 0.15f, color.a));
        }

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }

        if (material.HasProperty("_Mode"))
        {
            material.SetFloat("_Mode", 3f);
        }

        if (material.HasProperty("_SrcBlend"))
        {
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        }

        if (material.HasProperty("_DstBlend"))
        {
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        }

        if (material.HasProperty("_ZWrite"))
        {
            material.SetInt("_ZWrite", 0);
        }

        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = (int)RenderQueue.Transparent;
    }

    private Vector3 GetCurrentOriginPosition()
    {
        return _anchorPosition + (Vector3.up * _verticalOffset);
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
