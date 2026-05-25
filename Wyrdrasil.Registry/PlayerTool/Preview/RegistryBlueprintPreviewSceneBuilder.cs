using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.Rendering;
using Wyrdrasil.Construction.Models;
using Object = UnityEngine.Object;

namespace Wyrdrasil.Registry.PlayerTool.Preview;

public sealed class RegistryBlueprintPreviewSceneBuilder
{
    private readonly ManualLogSource _log;
    private GameObject? _hiddenRoot;

    public RegistryBlueprintPreviewSceneBuilder(ManualLogSource log)
    {
        _log = log;
    }

    public RegistryBlueprintPreviewScene Build(StructureBlueprintData blueprint, ZNetScene zNetScene)
    {
        EnsureHiddenRoot();

        var sceneRoot = new GameObject($"Wyrdrasil.BlueprintPreview.{SanitizeName(blueprint.Id)}");
        sceneRoot.transform.SetParent(_hiddenRoot!.transform, false);
        sceneRoot.transform.localPosition = Vector3.zero;
        sceneRoot.transform.localRotation = Quaternion.identity;
        sceneRoot.transform.localScale = Vector3.one;
        sceneRoot.SetActive(true);

        var renderers = new List<Renderer>();
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

            var pieceRoot = new GameObject($"PreviewPiece.{sourcePrefab.name}");
            pieceRoot.transform.SetParent(sceneRoot.transform, false);
            pieceRoot.transform.localPosition = pieceData.LocalPosition;
            pieceRoot.transform.localRotation = pieceData.LocalRotation;
            pieceRoot.transform.localScale = Vector3.one;

            var rendererCountBefore = renderers.Count;
            CloneVisualRenderers(sourcePrefab.transform, pieceRoot.transform, renderers);
            if (renderers.Count > rendererCountBefore)
            {
                visualPieceCount++;
            }
            else
            {
                Object.Destroy(pieceRoot);
            }
        }

        Bounds bounds;
        if (!TryComputeBounds(renderers, out bounds))
        {
            bounds = ComputeFallbackBounds(blueprint);
        }
        else
        {
            // Normalize the whole preview scene around the origin. This keeps camera maths stable
            // and makes framing independent from where the original blueprint was captured.
            sceneRoot.transform.position -= bounds.center;
            TryComputeBounds(renderers, out bounds);
        }

        return new RegistryBlueprintPreviewScene(sceneRoot, renderers, bounds, visualPieceCount);
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

    private void CloneVisualRenderers(Transform sourceRoot, Transform pieceRoot, ICollection<Renderer> renderers)
    {
        var meshRenderers = sourceRoot.GetComponentsInChildren<MeshRenderer>(true);
        for (var i = 0; i < meshRenderers.Length; i++)
        {
            var sourceRenderer = meshRenderers[i];
            var sourceFilter = sourceRenderer.GetComponent<MeshFilter>();
            if (sourceFilter == null || sourceFilter.sharedMesh == null)
            {
                continue;
            }

            var visual = CreateVisualObject(sourceRoot, sourceRenderer.transform, pieceRoot, sourceRenderer.name);
            var clonedFilter = visual.AddComponent<MeshFilter>();
            clonedFilter.sharedMesh = sourceFilter.sharedMesh;

            var clonedRenderer = visual.AddComponent<MeshRenderer>();
            clonedRenderer.sharedMaterials = sourceRenderer.sharedMaterials;
            ConfigureRenderer(clonedRenderer);
            renderers.Add(clonedRenderer);
        }

        var skinnedRenderers = sourceRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (var i = 0; i < skinnedRenderers.Length; i++)
        {
            var sourceRenderer = skinnedRenderers[i];
            if (sourceRenderer.sharedMesh == null)
            {
                continue;
            }

            var visual = CreateVisualObject(sourceRoot, sourceRenderer.transform, pieceRoot, sourceRenderer.name);
            var clonedFilter = visual.AddComponent<MeshFilter>();
            clonedFilter.sharedMesh = sourceRenderer.sharedMesh;

            var clonedRenderer = visual.AddComponent<MeshRenderer>();
            clonedRenderer.sharedMaterials = sourceRenderer.sharedMaterials;
            ConfigureRenderer(clonedRenderer);
            renderers.Add(clonedRenderer);
        }
    }

    private static GameObject CreateVisualObject(
        Transform sourceRoot,
        Transform sourceRendererTransform,
        Transform pieceRoot,
        string sourceName)
    {
        var visual = new GameObject($"Visual.{sourceName}");
        visual.transform.SetParent(pieceRoot, false);

        var localMatrix = sourceRoot.worldToLocalMatrix * sourceRendererTransform.localToWorldMatrix;
        visual.transform.localPosition = ExtractPosition(localMatrix);
        visual.transform.localRotation = ExtractRotation(localMatrix);
        visual.transform.localScale = ExtractScale(localMatrix);

        return visual;
    }

    private static void ConfigureRenderer(Renderer renderer)
    {
        renderer.enabled = true;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    private static Vector3 ExtractPosition(Matrix4x4 matrix)
    {
        var column = matrix.GetColumn(3);
        return new Vector3(column.x, column.y, column.z);
    }

    private static Quaternion ExtractRotation(Matrix4x4 matrix)
    {
        var forward = matrix.GetColumn(2);
        var upwards = matrix.GetColumn(1);
        if (forward.sqrMagnitude <= 0.0001f || upwards.sqrMagnitude <= 0.0001f)
        {
            return Quaternion.identity;
        }

        return Quaternion.LookRotation(forward, upwards);
    }

    private static Vector3 ExtractScale(Matrix4x4 matrix)
    {
        return new Vector3(
            matrix.GetColumn(0).magnitude,
            matrix.GetColumn(1).magnitude,
            matrix.GetColumn(2).magnitude);
    }

    private static bool TryComputeBounds(IReadOnlyList<Renderer> renderers, out Bounds bounds)
    {
        bounds = default(Bounds);
        var found = false;

        for (var i = 0; i < renderers.Count; i++)
        {
            var renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return found;
    }

    private static Bounds ComputeFallbackBounds(StructureBlueprintData blueprint)
    {
        if (blueprint.Pieces == null || blueprint.Pieces.Count == 0)
        {
            return new Bounds(Vector3.zero, Vector3.one * 4f);
        }

        var bounds = new Bounds(blueprint.Pieces[0].LocalPosition, Vector3.one);
        for (var i = 1; i < blueprint.Pieces.Count; i++)
        {
            bounds.Encapsulate(blueprint.Pieces[i].LocalPosition);
        }

        bounds.Expand(2f);
        return bounds;
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
