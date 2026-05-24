using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.Rendering;
using Wyrdrasil.Construction.Authoring;
using Wyrdrasil.Construction.Models;
using Object = UnityEngine.Object;

namespace Wyrdrasil.Registry.PlayerTool;

public sealed class RegistryPlayerToolBlueprintThumbnailService
{
    private sealed class ThumbnailCacheEntry
    {
        public string Signature { get; set; } = string.Empty;
        public Texture2D? Texture { get; set; }
        public Sprite? Sprite { get; set; }
    }

    private readonly ManualLogSource _log;
    private readonly IConstructionAuthoringApi _constructionAuthoringApi;
    private readonly Dictionary<string, ThumbnailCacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);

    private GameObject? _hiddenRoot;

    public RegistryPlayerToolBlueprintThumbnailService(
        ManualLogSource log,
        IConstructionAuthoringApi constructionAuthoringApi)
    {
        _log = log;
        _constructionAuthoringApi = constructionAuthoringApi;
    }

    public Sprite? TryGetThumbnail(string blueprintId, ZNetScene? zNetScene)
    {
        if (string.IsNullOrWhiteSpace(blueprintId) || zNetScene == null)
        {
            return null;
        }

        var blueprint = _constructionAuthoringApi
            .GetBlueprints()
            .FirstOrDefault(candidate => string.Equals(candidate.Id, blueprintId, StringComparison.OrdinalIgnoreCase));

        if (blueprint == null || blueprint.Pieces == null || blueprint.Pieces.Count == 0)
        {
            return null;
        }

        var signature = BuildBlueprintSignature(blueprint);
        if (_cache.TryGetValue(blueprint.Id, out var cached) &&
            string.Equals(cached.Signature, signature, StringComparison.Ordinal) &&
            cached.Sprite != null)
        {
            return cached.Sprite;
        }

        var generatedSprite = GenerateThumbnailSprite(blueprint, zNetScene);
        if (generatedSprite == null)
        {
            return cached?.Sprite;
        }

        if (cached != null)
        {
            DestroyCacheEntryAssets(cached);
        }
        else
        {
            cached = new ThumbnailCacheEntry();
            _cache[blueprint.Id] = cached;
        }

        cached.Signature = signature;
        cached.Texture = generatedSprite.texture;
        cached.Sprite = generatedSprite;

        return generatedSprite;
    }

    private Sprite? GenerateThumbnailSprite(StructureBlueprintData blueprint, ZNetScene zNetScene)
    {
        EnsureHiddenRoot();

        var renderRoot = new GameObject($"Wyrdrasil.BlueprintThumbnail.{SanitizeName(blueprint.Id)}");
        renderRoot.transform.SetParent(_hiddenRoot!.transform, false);
        // Keep the temporary visual-only clone close to the origin while rendering.
        // Rendering at very large coordinates can produce an apparently empty texture because of camera precision.
        renderRoot.transform.position = Vector3.zero;
        renderRoot.SetActive(true);

        RenderTexture? renderTexture = null;
        RenderTexture? previousActive = null;

        try
        {
            var visualPieceCount = 0;
            foreach (var pieceData in blueprint.Pieces.OrderBy(piece => piece.BuildOrder))
            {
                if (string.IsNullOrWhiteSpace(pieceData.PrefabName))
                {
                    continue;
                }

                var sourcePrefab = FindPrefab(zNetScene, pieceData.PrefabName);
                if (sourcePrefab == null)
                {
                    continue;
                }

                var pieceRoot = new GameObject($"Piece.{sourcePrefab.name}");
                pieceRoot.transform.SetParent(renderRoot.transform, false);
                pieceRoot.transform.localPosition = pieceData.LocalPosition;
                pieceRoot.transform.localRotation = pieceData.LocalRotation;
                pieceRoot.transform.localScale = Vector3.one;

                if (CloneVisualHierarchy(sourcePrefab.transform, pieceRoot.transform))
                {
                    visualPieceCount++;
                }
                else
                {
                    Object.Destroy(pieceRoot);
                }
            }

            if (visualPieceCount == 0)
            {
                _log.LogWarning(
                    $"Could not generate thumbnail for blueprint '{blueprint.DisplayName}' ({blueprint.Id}): no visual meshes were found.");
                return null;
            }

            if (!TryComputeRenderBounds(renderRoot, out var bounds))
            {
                bounds = new Bounds(renderRoot.transform.position, new Vector3(4f, 4f, 4f));
            }

            var resolution = RegistryPlayerToolConstants.BlueprintThumbnailResolution;
            var cameraGameObject = new GameObject("Wyrdrasil.BlueprintThumbnail.Camera");
            cameraGameObject.transform.SetParent(renderRoot.transform, false);
            var camera = cameraGameObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.orthographic = true;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 300f;
            camera.allowHDR = false;
            camera.allowMSAA = false;

            var lightGameObject = new GameObject("Wyrdrasil.BlueprintThumbnail.Light");
            lightGameObject.transform.SetParent(renderRoot.transform, false);
            var light = lightGameObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.65f;
            light.color = Color.white;
            light.shadows = LightShadows.None;

            var targetCenter = bounds.center;
            var renderDirection = new Vector3(-0.85f, 0.7f, -0.85f).normalized;
            var radius = Math.Max(bounds.extents.magnitude, 1f);
            var distance = Math.Max(radius * 3.25f, 8f);
            camera.transform.position = targetCenter - (renderDirection * distance);
            camera.transform.LookAt(targetCenter);
            camera.orthographicSize = ComputeOrthographicSize(bounds);
            light.transform.rotation = Quaternion.LookRotation(-renderDirection, Vector3.up);

            renderTexture = new RenderTexture(resolution, resolution, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 2
            };

            previousActive = RenderTexture.active;
            camera.targetTexture = renderTexture;
            RenderTexture.active = renderTexture;
            camera.Render();

            var texture = new Texture2D(resolution, resolution, TextureFormat.ARGB32, false, false)
            {
                name = $"WyrdrasilBlueprintThumbnail_{SanitizeName(blueprint.Id)}"
            };

            texture.ReadPixels(new Rect(0f, 0f, resolution, resolution), 0, 0, false);
            NormalizeVisibleAlpha(texture);
            if (!HasVisiblePixels(texture))
            {
                Object.Destroy(texture);
                texture = CreateSchematicThumbnail(blueprint, resolution);
                _log.LogWarning(
                    $"Rendered thumbnail for blueprint '{blueprint.DisplayName}' was empty; using schematic fallback thumbnail.");
            }

            texture.Apply(false, false);

            camera.targetTexture = null;
            RenderTexture.active = previousActive;
            Object.Destroy(renderTexture);
            renderTexture = null;

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                RegistryPlayerToolConstants.BlueprintThumbnailPixelsPerUnit);
        }
        catch (Exception exception)
        {
            _log.LogWarning(
                $"Failed to generate thumbnail for blueprint '{blueprint.DisplayName}' ({blueprint.Id}): {exception.Message}");
            return null;
        }
        finally
        {
            if (previousActive != null)
            {
                RenderTexture.active = previousActive;
            }

            if (renderTexture != null)
            {
                Object.Destroy(renderTexture);
            }

            Object.Destroy(renderRoot);
        }
    }

    private static GameObject? FindPrefab(ZNetScene zNetScene, string prefabName)
    {
        var prefab = zNetScene.GetPrefab(prefabName);
        if (prefab != null)
        {
            return prefab;
        }

        return zNetScene.m_prefabs.FirstOrDefault(candidate =>
            candidate != null &&
            string.Equals(candidate.name, prefabName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool CloneVisualHierarchy(Transform sourceRoot, Transform targetParent)
    {
        var visualCount = 0;
        CloneVisualNode(sourceRoot, targetParent, ref visualCount);
        return visualCount > 0;
    }

    private static void CloneVisualNode(Transform source, Transform targetParent, ref int visualCount)
    {
        var target = new GameObject(source.name);
        target.transform.SetParent(targetParent, false);
        target.transform.localPosition = source.localPosition;
        target.transform.localRotation = source.localRotation;
        target.transform.localScale = source.localScale;

        var meshFilter = source.GetComponent<MeshFilter>();
        var meshRenderer = source.GetComponent<MeshRenderer>();
        if (meshFilter != null && meshFilter.sharedMesh != null && meshRenderer != null)
        {
            var clonedFilter = target.AddComponent<MeshFilter>();
            clonedFilter.sharedMesh = meshFilter.sharedMesh;

            var clonedRenderer = target.AddComponent<MeshRenderer>();
            clonedRenderer.sharedMaterials = meshRenderer.sharedMaterials;
            clonedRenderer.shadowCastingMode = ShadowCastingMode.Off;
            clonedRenderer.receiveShadows = false;
            clonedRenderer.lightProbeUsage = LightProbeUsage.Off;
            clonedRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            clonedRenderer.enabled = meshRenderer.enabled;
            visualCount++;
        }

        var skinnedRenderer = source.GetComponent<SkinnedMeshRenderer>();
        if (skinnedRenderer != null && skinnedRenderer.sharedMesh != null)
        {
            var clonedSkinnedRenderer = target.AddComponent<SkinnedMeshRenderer>();
            clonedSkinnedRenderer.sharedMesh = skinnedRenderer.sharedMesh;
            clonedSkinnedRenderer.sharedMaterials = skinnedRenderer.sharedMaterials;
            clonedSkinnedRenderer.localBounds = skinnedRenderer.localBounds;
            clonedSkinnedRenderer.shadowCastingMode = ShadowCastingMode.Off;
            clonedSkinnedRenderer.receiveShadows = false;
            clonedSkinnedRenderer.lightProbeUsage = LightProbeUsage.Off;
            clonedSkinnedRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            clonedSkinnedRenderer.enabled = skinnedRenderer.enabled;
            visualCount++;
        }

        for (var i = 0; i < source.childCount; i++)
        {
            CloneVisualNode(source.GetChild(i), target.transform, ref visualCount);
        }
    }

    private static float ComputeOrthographicSize(Bounds bounds)
    {
        var horizontalSize = Math.Max(bounds.size.x, bounds.size.z) * 0.72f;
        var verticalSize = Math.Max(bounds.size.y, 1f) * 0.85f;
        return Math.Max(2.5f, Math.Max(horizontalSize, verticalSize));
    }

    private static void NormalizeVisibleAlpha(Texture2D texture)
    {
        var pixels = texture.GetPixels();
        for (var i = 0; i < pixels.Length; i++)
        {
            var pixel = pixels[i];
            var luminance = (pixel.r * 0.2126f) + (pixel.g * 0.7152f) + (pixel.b * 0.0722f);
            if (pixel.a < 0.05f && luminance > 0.025f)
            {
                pixel.a = 1f;
                pixels[i] = pixel;
            }
        }

        texture.SetPixels(pixels);
    }

    private static bool HasVisiblePixels(Texture2D texture)
    {
        var pixels = texture.GetPixels();
        for (var i = 0; i < pixels.Length; i++)
        {
            if (pixels[i].a > 0.08f)
            {
                return true;
            }
        }

        return false;
    }

    private static Texture2D CreateSchematicThumbnail(StructureBlueprintData blueprint, int resolution)
    {
        var texture = new Texture2D(resolution, resolution, TextureFormat.ARGB32, false, false)
        {
            name = $"WyrdrasilBlueprintSchematicThumbnail_{SanitizeName(blueprint.Id)}"
        };

        var pixels = new Color[resolution * resolution];
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = new Color(0f, 0f, 0f, 0f);
        }

        if (blueprint.Pieces == null || blueprint.Pieces.Count == 0)
        {
            texture.SetPixels(pixels);
            return texture;
        }

        var minX = blueprint.Pieces.Min(piece => piece.LocalPosition.x);
        var maxX = blueprint.Pieces.Max(piece => piece.LocalPosition.x);
        var minZ = blueprint.Pieces.Min(piece => piece.LocalPosition.z);
        var maxZ = blueprint.Pieces.Max(piece => piece.LocalPosition.z);
        var width = Math.Max(maxX - minX, 1f);
        var depth = Math.Max(maxZ - minZ, 1f);
        var scale = (resolution * 0.74f) / Math.Max(width, depth);
        var centerX = (minX + maxX) * 0.5f;
        var centerZ = (minZ + maxZ) * 0.5f;
        var footprintColor = new Color(0.92f, 0.72f, 0.38f, 0.95f);
        var highColor = new Color(1f, 0.88f, 0.55f, 1f);

        foreach (var piece in blueprint.Pieces)
        {
            var x = (int)(resolution * 0.5f + ((piece.LocalPosition.x - centerX) * scale));
            var y = (int)(resolution * 0.5f + ((piece.LocalPosition.z - centerZ) * scale));
            var normalizedHeight = Mathf.Clamp01((piece.LocalPosition.y + 4f) / 16f);
            var color = Color.Lerp(footprintColor, highColor, normalizedHeight);
            DrawFilledSquare(pixels, resolution, x, y, 3, color);
        }

        texture.SetPixels(pixels);
        return texture;
    }

    private static void DrawFilledSquare(Color[] pixels, int resolution, int centerX, int centerY, int radius, Color color)
    {
        for (var y = centerY - radius; y <= centerY + radius; y++)
        {
            if (y < 0 || y >= resolution)
            {
                continue;
            }

            for (var x = centerX - radius; x <= centerX + radius; x++)
            {
                if (x < 0 || x >= resolution)
                {
                    continue;
                }

                pixels[(y * resolution) + x] = color;
            }
        }
    }

    private static bool TryComputeRenderBounds(GameObject root, out Bounds bounds)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true)
            .Where(renderer => renderer != null && renderer.enabled)
            .ToArray();

        if (renderers.Length == 0)
        {
            bounds = default(Bounds);
            return false;
        }

        bounds = renderers[0].bounds;
        for (var i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return true;
    }

    private void EnsureHiddenRoot()
    {
        if (_hiddenRoot != null)
        {
            return;
        }

        _hiddenRoot = new GameObject(RegistryPlayerToolConstants.BlueprintThumbnailHiddenRootName);
        _hiddenRoot.SetActive(true);
        Object.DontDestroyOnLoad(_hiddenRoot);
    }

    private static string BuildBlueprintSignature(StructureBlueprintData blueprint)
    {
        return string.Join(
            "|",
            blueprint.Pieces
                .OrderBy(piece => piece.BuildOrder)
                .ThenBy(piece => piece.PieceId)
                .Select(piece => string.Join(",",
                    piece.PrefabName,
                    piece.LocalPosition.x.ToString("0.###", CultureInfo.InvariantCulture),
                    piece.LocalPosition.y.ToString("0.###", CultureInfo.InvariantCulture),
                    piece.LocalPosition.z.ToString("0.###", CultureInfo.InvariantCulture),
                    piece.LocalRotation.eulerAngles.x.ToString("0.###", CultureInfo.InvariantCulture),
                    piece.LocalRotation.eulerAngles.y.ToString("0.###", CultureInfo.InvariantCulture),
                    piece.LocalRotation.eulerAngles.z.ToString("0.###", CultureInfo.InvariantCulture))));
    }

    private static string SanitizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        var invalid = System.IO.Path.GetInvalidFileNameChars();
        var chars = value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        return new string(chars);
    }

    private static void DestroyCacheEntryAssets(ThumbnailCacheEntry entry)
    {
        if (entry.Sprite != null)
        {
            Object.Destroy(entry.Sprite);
            entry.Sprite = null;
        }

        if (entry.Texture != null)
        {
            Object.Destroy(entry.Texture);
            entry.Texture = null;
        }
    }
}
