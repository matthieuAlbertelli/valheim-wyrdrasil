using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;
using Wyrdrasil.Construction.Models;
using Wyrdrasil.Core.Services;

namespace Wyrdrasil.Construction.Services;

public sealed class ConstructionProjectGhostService
{
    private sealed class GhostPiece
    {
        public int ProjectId { get; }
        public int PieceId { get; }
        public GameObject Root { get; }
        public List<Renderer> Renderers { get; }

        public GhostPiece(int projectId, int pieceId, GameObject root, List<Renderer> renderers)
        {
            ProjectId = projectId;
            PieceId = pieceId;
            Root = root;
            Renderers = renderers;
        }
    }

    private readonly RegistryModeService _registryModeService;
    private readonly BlueprintCatalogService _blueprintCatalogService;
    private readonly ConstructionPlacementService _constructionPlacementService;
    private readonly ConstructionPieceBuildOrderService _constructionPieceBuildOrderService;
    private readonly ConstructionProjectService _constructionProjectService;
    private readonly Dictionary<string, GhostPiece> _ghostPiecesByKey = new();
    private readonly Dictionary<string, Material> _materialsByKey = new();
    private float _nextRefreshTime;

    public ConstructionProjectGhostService(
        RegistryModeService registryModeService,
        BlueprintCatalogService blueprintCatalogService,
        ConstructionPlacementService constructionPlacementService,
        ConstructionPieceBuildOrderService constructionPieceBuildOrderService,
        ConstructionProjectService constructionProjectService)
    {
        _registryModeService = registryModeService;
        _blueprintCatalogService = blueprintCatalogService;
        _constructionPlacementService = constructionPlacementService;
        _constructionPieceBuildOrderService = constructionPieceBuildOrderService;
        _constructionProjectService = constructionProjectService;
    }

    public void Update()
    {
        if (!_registryModeService.IsRegistryModeEnabled)
        {
            Reset();
            return;
        }

        if (Time.time < _nextRefreshTime)
        {
            return;
        }

        _nextRefreshTime = Time.time + 0.35f;
        RefreshAllProjectGhosts();
    }

    public void Reset()
    {
        foreach (var ghostPiece in _ghostPiecesByKey.Values)
        {
            if (ghostPiece.Root != null)
            {
                Object.Destroy(ghostPiece.Root);
            }
        }

        _ghostPiecesByKey.Clear();

        foreach (var material in _materialsByKey.Values)
        {
            if (material != null)
            {
                Object.Destroy(material);
            }
        }

        _materialsByKey.Clear();
    }

    private void RefreshAllProjectGhosts()
    {
        var expectedKeys = new HashSet<string>();

        foreach (var project in _constructionProjectService.Projects)
        {
            if (!_blueprintCatalogService.TryGetBlueprint(project.BlueprintId, out var blueprint))
            {
                continue;
            }

            _constructionProjectService.EnsurePieceProgress(project, blueprint);
            if (!_constructionPlacementService.TryResolveBlueprintPlacements(
                    blueprint,
                    project.OriginPosition,
                    project.OriginRotation,
                    out var placements,
                    out _))
            {
                continue;
            }

            _constructionPieceBuildOrderService.TrySelectNextBuildablePiece(
                project,
                blueprint,
                placements,
                out _,
                out _,
                out _);

            var placementsById = placements.ToDictionary(placement => placement.PieceId);
            foreach (var pieceProgress in project.Progress.Pieces)
            {
                if (pieceProgress.State == ConstructionPieceBuildState.Built)
                {
                    continue;
                }

                if (!placementsById.TryGetValue(pieceProgress.PieceId, out var placement))
                {
                    continue;
                }

                var key = BuildKey(project.Id, pieceProgress.PieceId);
                expectedKeys.Add(key);
                if (!_ghostPiecesByKey.TryGetValue(key, out var ghostPiece) || ghostPiece.Root == null)
                {
                    ghostPiece = CreateGhostPiece(project.Id, placement);
                    _ghostPiecesByKey[key] = ghostPiece;
                }

                ghostPiece.Root.transform.SetPositionAndRotation(placement.WorldPosition, placement.WorldRotation);
                ApplyGhostColor(ghostPiece, ResolveColor(pieceProgress));
            }
        }

        foreach (var staleKey in _ghostPiecesByKey.Keys.Where(key => !expectedKeys.Contains(key)).ToList())
        {
            if (_ghostPiecesByKey.TryGetValue(staleKey, out var staleGhost) && staleGhost.Root != null)
            {
                Object.Destroy(staleGhost.Root);
            }

            _ghostPiecesByKey.Remove(staleKey);
        }
    }

    private GhostPiece CreateGhostPiece(int projectId, ConstructionResolvedPiecePlacement placement)
    {
        var root = new GameObject($"{placement.Prefab.name}_ConstructionProjectGhost_{projectId}_{placement.PieceId}");
        CloneVisualHierarchy(placement.Prefab.transform, root.transform, isRoot: true);
        PrepareGhostHierarchy(root);
        var renderers = root.GetComponentsInChildren<Renderer>(true).ToList();
        return new GhostPiece(projectId, placement.PieceId, root, renderers);
    }

    private static Color ResolveColor(ConstructionPieceProgressData pieceProgress)
    {
        if (pieceProgress.State == ConstructionPieceBuildState.Missing)
        {
            return new Color(1f, 0.55f, 0.1f, 0.28f);
        }

        if (pieceProgress.State == ConstructionPieceBuildState.Blocked)
        {
            return new Color(1f, 0.15f, 0.1f, 0.28f);
        }

        if (pieceProgress.State == ConstructionPieceBuildState.Buildable)
        {
            return pieceProgress.LastStabilityLevel switch
            {
                ConstructionPieceStabilityLevel.Grounded => new Color(0.15f, 0.55f, 1f, 0.28f),
                ConstructionPieceStabilityLevel.Strong => new Color(0.2f, 1f, 0.35f, 0.28f),
                ConstructionPieceStabilityLevel.Acceptable => new Color(0.65f, 1f, 0.25f, 0.28f),
                _ => new Color(0.8f, 0.8f, 0.8f, 0.22f)
            };
        }

        return new Color(0.75f, 0.75f, 0.75f, 0.18f);
    }

    private void ApplyGhostColor(GhostPiece ghostPiece, Color color)
    {
        var material = GetMaterial(color);
        foreach (var renderer in ghostPiece.Renderers)
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

    private Material GetMaterial(Color color)
    {
        var key = $"{color.r:0.00}_{color.g:0.00}_{color.b:0.00}_{color.a:0.00}";
        if (_materialsByKey.TryGetValue(key, out var existing) && existing != null)
        {
            return existing;
        }

        var shader = Shader.Find("Legacy Shaders/Transparent/Diffuse")
                     ?? Shader.Find("Unlit/Color")
                     ?? Shader.Find("Standard")
                     ?? Shader.Find("Sprites/Default");

        var material = new Material(shader)
        {
            name = $"WyrdrasilConstructionProjectGhost_{key}"
        };
        ConfigureGhostMaterial(material, color);
        _materialsByKey[key] = material;
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

    private static void PrepareGhostHierarchy(GameObject root)
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

    private static string BuildKey(int projectId, int pieceId) => $"{projectId}:{pieceId}";
}
