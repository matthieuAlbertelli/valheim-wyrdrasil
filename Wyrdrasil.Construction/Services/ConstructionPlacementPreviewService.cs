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

    private static readonly Color ValidColor = new Color(0.2f, 1f, 0.35f, 0.30f);
    private static readonly Color InvalidColor = new Color(1f, 0.25f, 0.25f, 0.30f);
    private static readonly Color GroundedColor = new Color(0.15f, 0.55f, 1f, 0.32f);
    private static readonly Color StrongColor = new Color(0.2f, 1f, 0.35f, 0.30f);
    private static readonly Color AcceptableColor = new Color(0.65f, 1f, 0.25f, 0.28f);
    private static readonly Color WeakColor = new Color(1f, 0.8f, 0.15f, 0.28f);
    private static readonly Color BlockedColor = new Color(1f, 0.15f, 0.1f, 0.32f);
    private static readonly Color PendingColor = new Color(0.75f, 0.75f, 0.75f, 0.18f);
    private const float RotationStepDegrees = 45f;

    private readonly BlueprintCatalogService _blueprintCatalogService;
    private readonly ConstructionPlacementService _constructionPlacementService;
    private readonly ConstructionProjectService _constructionProjectService;
    private readonly ConstructionPreviewStabilitySchedulerService _constructionPreviewStabilitySchedulerService;
    private readonly ConstructionDebugLogService _debugLogService;
    private readonly List<PreviewPiece> _previewPieces = new();

    private StructureBlueprintData? _activeBlueprint;
    private Vector3 _anchorPosition;
    private float _verticalOffset;
    private int _rotationStepIndex;
    private bool _isPlacementValid;
    private string _validationMessage = "No active construction preview.";
    private readonly Dictionary<string, Material> _ghostMaterialsByKey = new();

    public ConstructionPlacementPreviewService(
        BlueprintCatalogService blueprintCatalogService,
        ConstructionPlacementService constructionPlacementService,
        ConstructionProjectService constructionProjectService,
        ConstructionPreviewStabilitySchedulerService constructionPreviewStabilitySchedulerService,
        ConstructionDebugLogService debugLogService)
    {
        _blueprintCatalogService = blueprintCatalogService;
        _constructionPlacementService = constructionPlacementService;
        _constructionProjectService = constructionProjectService;
        _constructionPreviewStabilitySchedulerService = constructionPreviewStabilitySchedulerService;
        _debugLogService = debugLogService;
    }

    public bool IsPreviewActive => _activeBlueprint != null;
    public string ActiveBlueprintId => _activeBlueprint?.Id ?? string.Empty;
    public string StatusLabel => !IsPreviewActive
        ? "Construction preview inactive."
        : $"Construction preview: {ActiveBlueprintId} | Valid: {(_isPlacementValid ? "Yes" : "No")} | Y Offset: {_verticalOffset:0.0} | {_validationMessage} | {_constructionPreviewStabilitySchedulerService.StatusLabel}";
    public string ControlsLabel => "Construction preview: Molette = pivoter | Shift + molette = hauteur | Clic gauche = lancer le chantier | Clic droit = annuler";

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

        _verticalOffset += scrollDelta > 0f ? 0.5f : -0.5f;
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
        CancelPreviewObjectsOnly();
        DestroyGhostMaterials();
        _activeBlueprint = null;
        _anchorPosition = Vector3.zero;
        _verticalOffset = 0f;
        _rotationStepIndex = 0;
        _isPlacementValid = false;
        _validationMessage = "No active construction preview.";
        _constructionPreviewStabilitySchedulerService.Reset();
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
            _constructionPreviewStabilitySchedulerService.Reset();
            ApplyPreviewColor(isValid: false);
            return false;
        }

        EnsurePreviewObjects(placements);
        UpdatePreviewTransforms(placements);

        _isPlacementValid = EvaluateBlockingOverlaps(out _validationMessage);
        if (_isPlacementValid)
        {
            var now = Time.realtimeSinceStartup;
            _constructionPreviewStabilitySchedulerService.RequestAnalysis(
                _activeBlueprint,
                placements,
                GetCurrentOriginPosition(),
                GetCurrentRotation(),
                now);
            _constructionPreviewStabilitySchedulerService.Tick(now);

            if (_constructionPreviewStabilitySchedulerService.TryGetCurrentEvaluations(out var previewEvaluations))
            {
                ApplyPerPiecePreviewColors(previewEvaluations);
            }
            else
            {
                ApplyPendingPreviewColor();
            }
        }
        else
        {
            _constructionPreviewStabilitySchedulerService.Reset();
            ApplyPreviewColor(isValid: false);
        }

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
            var root = CreatePreviewVisualRoot(placement.Prefab, placement.PieceId);
            root.transform.SetPositionAndRotation(placement.WorldPosition, placement.WorldRotation);
            var renderers = root.GetComponentsInChildren<Renderer>(true).ToList();
            _previewPieces.Add(new PreviewPiece(placement.PieceId, root, renderers));
        }
    }

    private GameObject CreatePreviewVisualRoot(GameObject prefab, int pieceId)
    {
        var root = new GameObject($"{prefab.name}_ConstructionPreview_{pieceId}");
        CloneVisualHierarchy(prefab.transform, root.transform, isRoot: true);
        PrepareGhostHierarchy(root);
        return root;
    }

    private static void CloneVisualHierarchy(Transform source, Transform parent, bool isRoot = false)
    {
        Transform currentTransform;
        GameObject currentObject;

        if (isRoot)
        {
            currentTransform = parent;
            currentObject = parent.gameObject;
            currentTransform.localPosition = Vector3.zero;
            currentTransform.localRotation = Quaternion.identity;
            currentTransform.localScale = Vector3.one;
        }
        else
        {
            currentObject = new GameObject(source.name);
            currentTransform = currentObject.transform;
            currentTransform.SetParent(parent, false);
            currentTransform.localPosition = source.localPosition;
            currentTransform.localRotation = source.localRotation;
            currentTransform.localScale = source.localScale;
        }

        if (source.TryGetComponent<MeshFilter>(out var sourceMeshFilter) && sourceMeshFilter.sharedMesh != null)
        {
            var targetMeshFilter = currentObject.AddComponent<MeshFilter>();
            targetMeshFilter.sharedMesh = sourceMeshFilter.sharedMesh;
        }

        if (source.TryGetComponent<MeshRenderer>(out var sourceMeshRenderer))
        {
            var targetMeshRenderer = currentObject.AddComponent<MeshRenderer>();
            targetMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            targetMeshRenderer.receiveShadows = false;
            targetMeshRenderer.sharedMaterials = new Material[sourceMeshRenderer.sharedMaterials.Length];
        }

        if (source.TryGetComponent<LineRenderer>(out var sourceLineRenderer))
        {
            var targetLineRenderer = currentObject.AddComponent<LineRenderer>();
            targetLineRenderer.positionCount = sourceLineRenderer.positionCount;
            var positions = new Vector3[sourceLineRenderer.positionCount];
            sourceLineRenderer.GetPositions(positions);
            targetLineRenderer.SetPositions(positions);
            targetLineRenderer.widthMultiplier = sourceLineRenderer.widthMultiplier;
            targetLineRenderer.alignment = sourceLineRenderer.alignment;
            targetLineRenderer.useWorldSpace = false;
            targetLineRenderer.loop = sourceLineRenderer.loop;
            targetLineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            targetLineRenderer.receiveShadows = false;
        }

        foreach (Transform child in source)
        {
            CloneVisualHierarchy(child, currentTransform);
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

    private void ApplyPreviewColor(bool isValid)
    {
        var color = isValid ? ValidColor : InvalidColor;
        foreach (var previewPiece in _previewPieces)
        {
            ApplyPreviewPieceColor(previewPiece, color);
        }
    }

    private void ApplyPendingPreviewColor()
    {
        foreach (var previewPiece in _previewPieces)
        {
            ApplyPreviewPieceColor(previewPiece, PendingColor);
        }
    }

    private void ApplyPerPiecePreviewColors(IReadOnlyDictionary<int, ConstructionPreviewPieceEvaluation> evaluationsByPieceId)
    {
        foreach (var previewPiece in _previewPieces)
        {
            var color = evaluationsByPieceId.TryGetValue(previewPiece.PieceId, out var evaluation)
                ? ResolvePreviewPieceColor(evaluation)
                : PendingColor;
            ApplyPreviewPieceColor(previewPiece, color);
        }
    }

    private static Color ResolvePreviewPieceColor(ConstructionPreviewPieceEvaluation evaluation)
    {
        if (evaluation.State == ConstructionPieceBuildState.Blocked)
        {
            return BlockedColor;
        }

        if (evaluation.State == ConstructionPieceBuildState.Pending)
        {
            return PendingColor;
        }

        return evaluation.StabilityLevel switch
        {
            ConstructionPieceStabilityLevel.Grounded => GroundedColor,
            ConstructionPieceStabilityLevel.Strong => StrongColor,
            ConstructionPieceStabilityLevel.Acceptable => AcceptableColor,
            ConstructionPieceStabilityLevel.Weak => WeakColor,
            ConstructionPieceStabilityLevel.Unsupported => BlockedColor,
            _ => PendingColor
        };
    }

    private void ApplyPreviewPieceColor(PreviewPiece previewPiece, Color color)
    {
        var material = GetGhostMaterial(color);
        foreach (var renderer in previewPiece.Renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            if (renderer is LineRenderer lineRenderer)
            {
                lineRenderer.startColor = color;
                lineRenderer.endColor = color;
                continue;
            }

            var materialCount = renderer.sharedMaterials.Length;
            if (materialCount <= 0)
            {
                renderer.sharedMaterial = material;
                continue;
            }

            var replacements = new Material[materialCount];
            for (var index = 0; index < materialCount; index++)
            {
                replacements[index] = material;
            }

            renderer.sharedMaterials = replacements;
        }
    }

    private Material GetGhostMaterial(Color color)
    {
        var key = $"{color.r:0.00}_{color.g:0.00}_{color.b:0.00}_{color.a:0.00}";
        if (_ghostMaterialsByKey.TryGetValue(key, out var existing) && existing != null)
        {
            return existing;
        }

        var material = CreateGhostMaterial(color);
        material.name = $"WyrdrasilConstructionPreviewGhost_{key}";
        _ghostMaterialsByKey[key] = material;
        return material;
    }

    private static Material CreateGhostMaterial(Color color)
    {
        var shader = Shader.Find("Legacy Shaders/Transparent/Diffuse")
                     ?? Shader.Find("Unlit/Color")
                     ?? Shader.Find("Standard")
                     ?? Shader.Find("Sprites/Default");

        var material = new Material(shader)
        {
            name = $"WyrdrasilConstructionGhost_{shader?.name ?? "Fallback"}"
        };

        ConfigureGhostMaterial(material, color);
        return material;
    }

    private static void ConfigureGhostMaterial(Material material, Color color)
    {
        if (material.HasProperty("_Color"))
        {
            material.color = color;
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_EmissionColor"))
        {
            material.SetColor("_EmissionColor", new Color(color.r * 0.10f, color.g * 0.10f, color.b * 0.10f, 1f));
        }

        if (material.HasProperty("_Mode"))
        {
            material.SetFloat("_Mode", 3f);
        }

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }

        if (material.HasProperty("_SrcBlend"))
        {
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        }

        if (material.HasProperty("_DstBlend"))
        {
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        }

        if (material.HasProperty("_ZWrite"))
        {
            material.SetInt("_ZWrite", 0);
        }

        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
    }

    private void DestroyGhostMaterials()
    {
        foreach (var material in _ghostMaterialsByKey.Values)
        {
            if (material != null)
            {
                Object.Destroy(material);
            }
        }

        _ghostMaterialsByKey.Clear();
    }

    private Vector3 GetCurrentOriginPosition()
    {
        return _anchorPosition + new Vector3(0f, _verticalOffset, 0f);
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