using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BepInEx.Logging;
using UnityEngine;
using Wyrdrasil.Construction.Authoring;
using Wyrdrasil.Construction.Models;
using Wyrdrasil.Registry.PlayerTool.Preview;
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
    private readonly RegistryBlueprintPreviewSceneBuilder _previewSceneBuilder;
    private readonly RegistryBlueprintPreviewRenderer _previewRenderer;
    private readonly Dictionary<string, ThumbnailCacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);

    public RegistryPlayerToolBlueprintThumbnailService(
        ManualLogSource log,
        IConstructionAuthoringApi constructionAuthoringApi)
    {
        _log = log;
        _constructionAuthoringApi = constructionAuthoringApi;
        _previewSceneBuilder = new RegistryBlueprintPreviewSceneBuilder(log);
        _previewRenderer = new RegistryBlueprintPreviewRenderer();
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
        try
        {
            using (var previewScene = _previewSceneBuilder.Build(blueprint, zNetScene))
            {
                var renderedSprite = _previewRenderer.Render(previewScene, blueprint.Id);
                if (renderedSprite != null)
                {
                    return renderedSprite;
                }
            }

            _log.LogWarning(
                $"Rendered preview for blueprint '{blueprint.DisplayName}' ({blueprint.Id}) was not usable; using schematic preview.");
            return _previewRenderer.RenderSchematic(blueprint, blueprint.Id);
        }
        catch (Exception exception)
        {
            _log.LogWarning(
                $"Failed to generate preview thumbnail for blueprint '{blueprint.DisplayName}' ({blueprint.Id}): {exception.Message}");

            try
            {
                return _previewRenderer.RenderSchematic(blueprint, blueprint.Id);
            }
            catch (Exception fallbackException)
            {
                _log.LogWarning(
                    $"Failed to generate schematic preview for blueprint '{blueprint.DisplayName}' ({blueprint.Id}): {fallbackException.Message}");
                return null;
            }
        }
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
