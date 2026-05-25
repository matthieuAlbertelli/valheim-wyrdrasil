using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Wyrdrasil.Construction.Models;
using Object = UnityEngine.Object;

namespace Wyrdrasil.Registry.PlayerTool.Preview;

public sealed class RegistryBlueprintPreviewRenderer
{
    private const float MinimumUsefulPixelFillRatio = 0.012f;
    private const float MinimumUsefulBoundingBoxRatio = 0.18f;
    private const float TargetFillRatio = 0.88f;

    public Sprite? Render(RegistryBlueprintPreviewScene scene, string spriteName)
    {
        if (scene == null || !scene.HasRenderableContent)
        {
            return null;
        }

        var resolution = RegistryPlayerToolConstants.BlueprintThumbnailResolution;
        RenderTexture? renderTexture = null;
        RenderTexture? previousActive = null;
        Texture2D? texture = null;

        var cameraGameObject = new GameObject("Wyrdrasil.BlueprintPreview.Camera");
        var keyLightGameObject = new GameObject("Wyrdrasil.BlueprintPreview.KeyLight");
        var fillLightGameObject = new GameObject("Wyrdrasil.BlueprintPreview.FillLight");

        try
        {
            cameraGameObject.transform.SetParent(scene.Root.transform, false);
            keyLightGameObject.transform.SetParent(scene.Root.transform, false);
            fillLightGameObject.transform.SetParent(scene.Root.transform, false);

            var camera = cameraGameObject.AddComponent<Camera>();
            ConfigureCamera(camera);

            var requestedViewDirection = Vector3.zero;
            var layout = RegistryBlueprintPreviewCameraFraming.Compute(scene.Bounds, requestedViewDirection);
            camera.transform.position = layout.Position;
            camera.transform.rotation = layout.Rotation;
            camera.orthographicSize = layout.OrthographicSize;
            camera.nearClipPlane = layout.NearClipPlane;
            camera.farClipPlane = layout.FarClipPlane;

            var effectiveViewDirection = (layout.Target - layout.Position).normalized;
            ConfigureLight(keyLightGameObject.AddComponent<Light>(), Quaternion.LookRotation(-effectiveViewDirection, Vector3.up), 1.75f);
            ConfigureLight(fillLightGameObject.AddComponent<Light>(), Quaternion.Euler(45f, -35f, 0f), 0.55f);

            renderTexture = new RenderTexture(resolution, resolution, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 2
            };

            previousActive = RenderTexture.active;
            camera.targetTexture = renderTexture;
            RenderTexture.active = renderTexture;
            camera.Render();

            texture = new Texture2D(resolution, resolution, TextureFormat.ARGB32, false, false)
            {
                name = $"WyrdrasilBlueprintPreview_{SanitizeName(spriteName)}"
            };

            texture.ReadPixels(new Rect(0f, 0f, resolution, resolution), 0, 0, false);
            NormalizeVisibleAlpha(texture);

            if (!IsRenderUseful(texture))
            {
                Object.Destroy(texture);
                texture = null;
                return null;
            }

            FitVisibleContent(texture, TargetFillRatio);
            texture.Apply(false, false);

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                RegistryPlayerToolConstants.BlueprintThumbnailPixelsPerUnit);
        }
        finally
        {
            RenderTexture.active = previousActive;

            if (renderTexture != null)
            {
                Object.Destroy(renderTexture);
            }

            Object.Destroy(cameraGameObject);
            Object.Destroy(keyLightGameObject);
            Object.Destroy(fillLightGameObject);
        }
    }

    public Sprite RenderSchematic(StructureBlueprintData blueprint, string spriteName)
    {
        var resolution = RegistryPlayerToolConstants.BlueprintThumbnailResolution;
        var texture = new Texture2D(resolution, resolution, TextureFormat.ARGB32, false, false)
        {
            name = $"WyrdrasilBlueprintPreviewSchematic_{SanitizeName(spriteName)}"
        };

        var pixels = new Color[resolution * resolution];
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = new Color(0f, 0f, 0f, 0f);
        }

        var projected = ProjectBlueprintPieces(blueprint).ToList();
        if (projected.Count == 0)
        {
            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return CreateSprite(texture);
        }

        var minX = projected.Min(point => point.x);
        var maxX = projected.Max(point => point.x);
        var minY = projected.Min(point => point.y);
        var maxY = projected.Max(point => point.y);
        var width = Math.Max(maxX - minX, 1f);
        var height = Math.Max(maxY - minY, 1f);
        var targetSize = resolution * 0.88f;
        var scale = targetSize / Math.Max(width, height);
        var centerX = (minX + maxX) * 0.5f;
        var centerY = (minY + maxY) * 0.5f;
        var radius = Math.Max(2, resolution / 52);

        for (var i = 0; i < projected.Count; i++)
        {
            var point = projected[i];
            var x = Mathf.RoundToInt((resolution * 0.5f) + ((point.x - centerX) * scale));
            var y = Mathf.RoundToInt((resolution * 0.5f) + ((point.y - centerY) * scale));
            var heightT = Mathf.Clamp01((point.z + 4f) / 18f);
            var color = Color.Lerp(
                new Color(0.75f, 0.58f, 0.32f, 0.92f),
                new Color(1f, 0.86f, 0.48f, 1f),
                heightT);

            DrawFilledDiamond(pixels, resolution, x, y, radius, color);
        }

        DrawSoftOutline(pixels, resolution);
        texture.SetPixels(pixels);
        texture.Apply(false, false);
        return CreateSprite(texture);
    }

    private static void ConfigureCamera(Camera camera)
    {
        camera.enabled = false;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        camera.orthographic = true;
        camera.allowHDR = false;
        camera.allowMSAA = false;
    }

    private static void ConfigureLight(Light light, Quaternion rotation, float intensity)
    {
        light.type = LightType.Directional;
        light.color = Color.white;
        light.intensity = intensity;
        light.shadows = LightShadows.None;
        light.transform.rotation = rotation;
    }

    private static IEnumerable<Vector3> ProjectBlueprintPieces(StructureBlueprintData blueprint)
    {
        if (blueprint.Pieces == null)
        {
            yield break;
        }

        for (var i = 0; i < blueprint.Pieces.Count; i++)
        {
            var position = blueprint.Pieces[i].LocalPosition;
            var projectedX = (position.x - position.z) * 0.7071f;
            var projectedY = ((position.x + position.z) * 0.34f) + (position.y * 0.82f);
            yield return new Vector3(projectedX, projectedY, position.y);
        }
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

    private static bool IsRenderUseful(Texture2D texture)
    {
        var pixels = texture.GetPixels();
        if (!TryFindVisiblePixelBounds(pixels, texture.width, texture.height, out var minX, out var minY, out var maxX, out var maxY, out var visibleCount))
        {
            return false;
        }

        var visibleFillRatio = (float)visibleCount / pixels.Length;
        if (visibleFillRatio < MinimumUsefulPixelFillRatio)
        {
            return false;
        }

        var maxVisibleExtent = Math.Max(maxX - minX + 1, maxY - minY + 1);
        var boundingBoxRatio = (float)maxVisibleExtent / Math.Min(texture.width, texture.height);
        return boundingBoxRatio >= MinimumUsefulBoundingBoxRatio;
    }

    private static bool TryFindVisiblePixelBounds(
        Color[] pixels,
        int width,
        int height,
        out int minX,
        out int minY,
        out int maxX,
        out int maxY,
        out int visibleCount)
    {
        minX = width;
        minY = height;
        maxX = -1;
        maxY = -1;
        visibleCount = 0;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var pixel = pixels[(y * width) + x];
                if (pixel.a <= 0.08f)
                {
                    continue;
                }

                visibleCount++;
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        return maxX >= minX && maxY >= minY;
    }

    private static void FitVisibleContent(Texture2D texture, float fillRatio)
    {
        var sourcePixels = texture.GetPixels();
        if (!TryFindVisiblePixelBounds(sourcePixels, texture.width, texture.height, out var minX, out var minY, out var maxX, out var maxY, out _))
        {
            return;
        }

        var sourceWidth = maxX - minX + 1;
        var sourceHeight = maxY - minY + 1;
        if (sourceWidth <= 0 || sourceHeight <= 0)
        {
            return;
        }

        var destinationSize = Math.Max(1, texture.width);
        var destinationPixels = new Color[sourcePixels.Length];
        for (var i = 0; i < destinationPixels.Length; i++)
        {
            destinationPixels[i] = new Color(0f, 0f, 0f, 0f);
        }

        var scaledTarget = destinationSize * Mathf.Clamp(fillRatio, 0.5f, 0.96f);
        var scale = scaledTarget / Math.Max(sourceWidth, sourceHeight);
        var outputWidth = Math.Max(1, Mathf.RoundToInt(sourceWidth * scale));
        var outputHeight = Math.Max(1, Mathf.RoundToInt(sourceHeight * scale));
        var offsetX = (destinationSize - outputWidth) / 2;
        var offsetY = (destinationSize - outputHeight) / 2;

        for (var y = 0; y < outputHeight; y++)
        {
            var sourceY = minY + ((y + 0.5f) / scale);
            var sampleY = Mathf.Clamp(Mathf.FloorToInt(sourceY), minY, maxY);
            for (var x = 0; x < outputWidth; x++)
            {
                var sourceX = minX + ((x + 0.5f) / scale);
                var sampleX = Mathf.Clamp(Mathf.FloorToInt(sourceX), minX, maxX);
                var sourceIndex = (sampleY * texture.width) + sampleX;
                var destinationIndex = ((offsetY + y) * texture.width) + (offsetX + x);
                if (destinationIndex >= 0 && destinationIndex < destinationPixels.Length)
                {
                    destinationPixels[destinationIndex] = sourcePixels[sourceIndex];
                }
            }
        }

        texture.SetPixels(destinationPixels);
    }

    private static void DrawFilledDiamond(Color[] pixels, int resolution, int centerX, int centerY, int radius, Color color)
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

                if (Math.Abs(x - centerX) + Math.Abs(y - centerY) > radius)
                {
                    continue;
                }

                BlendPixel(pixels, resolution, x, y, color);
            }
        }
    }

    private static void DrawSoftOutline(Color[] pixels, int resolution)
    {
        var source = new Color[pixels.Length];
        Array.Copy(pixels, source, pixels.Length);
        var outline = new Color(0.12f, 0.08f, 0.04f, 0.55f);

        for (var y = 1; y < resolution - 1; y++)
        {
            for (var x = 1; x < resolution - 1; x++)
            {
                var index = (y * resolution) + x;
                if (source[index].a > 0.05f)
                {
                    continue;
                }

                if (source[index - 1].a > 0.05f ||
                    source[index + 1].a > 0.05f ||
                    source[index - resolution].a > 0.05f ||
                    source[index + resolution].a > 0.05f)
                {
                    pixels[index] = outline;
                }
            }
        }
    }

    private static void BlendPixel(Color[] pixels, int resolution, int x, int y, Color color)
    {
        var index = (y * resolution) + x;
        var existing = pixels[index];
        if (existing.a <= 0.01f || color.a >= existing.a)
        {
            pixels[index] = color;
        }
    }

    private static Sprite CreateSprite(Texture2D texture)
    {
        return Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            RegistryPlayerToolConstants.BlueprintThumbnailPixelsPerUnit);
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
}
