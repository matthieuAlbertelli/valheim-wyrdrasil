using System;
using System.IO;
using BepInEx.Logging;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Wyrdrasil.Registry.PlayerTool.Visual;

public sealed class RegistryPlayerToolVisualBundleService
{
    private readonly ManualLogSource _log;
    private readonly string _pluginFolder;
    private readonly RegistryPlayerToolVisualConfig _visualConfig;

    private bool _loadAttempted;
    private AssetBundle? _assetBundle;
    private GameObject? _visualPrefab;
    private Sprite? _icon;

    public RegistryPlayerToolVisualBundleService(
        ManualLogSource log,
        string pluginLocation,
        RegistryPlayerToolVisualConfig visualConfig)
    {
        _log = log;
        _pluginFolder = ResolvePluginFolder(pluginLocation);
        _visualConfig = visualConfig;
    }

    public bool IsVisualAvailable
    {
        get
        {
            EnsureLoaded();
            return _visualPrefab != null;
        }
    }

    public bool TryApplyVisual(GameObject itemPrefab)
    {
        EnsureLoaded();

        if (_visualPrefab == null)
        {
            _log.LogError(
                $"Cannot create '{RegistryPlayerToolConstants.ItemPrefabName}': custom visual prefab " +
                $"'{RegistryPlayerToolConstants.VisualPrefabName}' was not loaded from AssetBundle " +
                $"'{RegistryPlayerToolConstants.VisualAssetBundleName}'.");
            return false;
        }

        // Capture the vanilla renderers before adding the custom visual.
        // Otherwise the freshly instantiated ring renderers would be disabled as well.
        var oldRenderers = itemPrefab.GetComponentsInChildren<Renderer>(true);
        var visualParent = ResolveVisualParent(itemPrefab.transform, oldRenderers);

        foreach (var renderer in oldRenderers)
        {
            if (renderer != null)
            {
                renderer.enabled = false;
            }
        }

        var visual = Object.Instantiate(_visualPrefab, visualParent, false);
        visual.name = RegistryPlayerToolConstants.VisualPrefabName;

        SanitizeVisualInstance(visual);
        ApplyConfiguredTransform(visual.transform);
        EnsureVisualRenderersAreEnabled(visual);
        visual.SetActive(true);

        _log.LogInfo(
            $"Applied custom visual '{RegistryPlayerToolConstants.VisualPrefabName}' to " +
            $"'{RegistryPlayerToolConstants.ItemPrefabName}' under parent '{GetTransformPath(visualParent)}'. " +
            $"Disabled vanilla renderer count={oldRenderers.Length}.");
        return true;
    }

    public bool TryGetIcon(out Sprite icon)
    {
        EnsureLoaded();

        if (_icon == null)
        {
            icon = null!;
            return false;
        }

        icon = _icon;
        return true;
    }

    private void EnsureLoaded()
    {
        if (_loadAttempted)
        {
            return;
        }

        _loadAttempted = true;

        var bundlePath = ResolveBundlePath();
        if (string.IsNullOrWhiteSpace(bundlePath))
        {
            _log.LogError(
                $"AssetBundle '{RegistryPlayerToolConstants.VisualAssetBundleName}' was not found next to the plugin DLL " +
                $"or in the plugin Assets folder. Expected one of: '{Path.Combine(_pluginFolder, RegistryPlayerToolConstants.VisualAssetBundleName)}' " +
                $"or '{Path.Combine(_pluginFolder, "Assets", RegistryPlayerToolConstants.VisualAssetBundleName)}'.");
            return;
        }

        _assetBundle = AssetBundle.LoadFromFile(bundlePath);
        if (_assetBundle == null)
        {
            _log.LogError($"Failed to load AssetBundle '{RegistryPlayerToolConstants.VisualAssetBundleName}' from '{bundlePath}'.");
            return;
        }

        _log.LogInfo($"AssetBundle loaded successfully: '{bundlePath}'.");

        _visualPrefab = _assetBundle.LoadAsset<GameObject>(RegistryPlayerToolConstants.VisualPrefabName);
        if (_visualPrefab == null)
        {
            _log.LogError(
                $"Failed to load prefab '{RegistryPlayerToolConstants.VisualPrefabName}' from AssetBundle " +
                $"'{RegistryPlayerToolConstants.VisualAssetBundleName}'.");
            return;
        }

        _log.LogInfo($"{RegistryPlayerToolConstants.VisualPrefabName} loaded successfully.");

        _icon = _assetBundle.LoadAsset<Sprite>(RegistryPlayerToolConstants.IconAssetName);
        if (_icon != null)
        {
            _log.LogInfo($"{RegistryPlayerToolConstants.IconAssetName} loaded successfully.");
        }
    }

    private Transform ResolveVisualParent(Transform itemRoot, Renderer[] oldRenderers)
    {
        var configuredParentName = _visualConfig.ParentTransformName;
        if (!string.IsNullOrWhiteSpace(configuredParentName))
        {
            var configuredParent = FindChildRecursive(itemRoot, configuredParentName);
            if (configuredParent != null)
            {
                return configuredParent;
            }

            _log.LogWarning(
                $"Configured visual parent transform '{configuredParentName}' was not found under " +
                $"'{itemRoot.name}'. Falling back to automatic visual parent resolution.");
        }

        var attachParent = FindFirstChildRecursive(
            itemRoot,
            "attach",
            "Attach",
            "attach_skin",
            "AttachSkin",
            "attach_right",
            "AttachRight",
            "RightHand",
            "right_hand");
        if (attachParent != null)
        {
            return attachParent;
        }

        var rendererParent = ResolveRendererParent(oldRenderers);
        if (rendererParent != null)
        {
            return rendererParent;
        }

        return itemRoot;
    }

    private static Transform? ResolveRendererParent(Renderer[] oldRenderers)
    {
        foreach (var renderer in oldRenderers)
        {
            if (renderer == null)
            {
                continue;
            }

            var rendererTransform = renderer.transform;
            if (rendererTransform == null)
            {
                continue;
            }

            return rendererTransform.parent != null ? rendererTransform.parent : rendererTransform;
        }

        return null;
    }

    private static Transform? FindFirstChildRecursive(Transform root, params string[] names)
    {
        foreach (var name in names)
        {
            var match = FindChildRecursive(root, name);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static Transform? FindChildRecursive(Transform root, string name)
    {
        if (string.Equals(root.name, name, StringComparison.OrdinalIgnoreCase))
        {
            return root;
        }

        for (var i = 0; i < root.childCount; i++)
        {
            var match = FindChildRecursive(root.GetChild(i), name);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private void SanitizeVisualInstance(GameObject visual)
    {
        var disabledColliderCount = 0;
        foreach (var collider in visual.GetComponentsInChildren<Collider>(true))
        {
            if (collider == null)
            {
                continue;
            }

            collider.enabled = false;
            disabledColliderCount++;
        }

        var sanitizedRigidbodyCount = 0;
        foreach (var rigidbody in visual.GetComponentsInChildren<Rigidbody>(true))
        {
            if (rigidbody == null)
            {
                continue;
            }

            rigidbody.isKinematic = true;
            rigidbody.detectCollisions = false;
            rigidbody.useGravity = false;
            sanitizedRigidbodyCount++;
        }

        if (disabledColliderCount > 0 || sanitizedRigidbodyCount > 0)
        {
            _log.LogInfo(
                $"Sanitized custom visual physics: disabled colliders={disabledColliderCount}, " +
                $"sanitized rigidbodies={sanitizedRigidbodyCount}.");
        }
    }

    private static void EnsureVisualRenderersAreEnabled(GameObject visual)
    {
        foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer != null)
            {
                renderer.enabled = true;
            }
        }
    }

    private void ApplyConfiguredTransform(Transform visualTransform)
    {
        visualTransform.localPosition = _visualConfig.LocalPosition;
        visualTransform.localEulerAngles = _visualConfig.LocalEulerAngles;
        visualTransform.localScale = _visualConfig.LocalScale;
    }

    private string? ResolveBundlePath()
    {
        var directPath = Path.Combine(_pluginFolder, RegistryPlayerToolConstants.VisualAssetBundleName);
        if (File.Exists(directPath))
        {
            return directPath;
        }

        var assetFolderPath = Path.Combine(_pluginFolder, "Assets", RegistryPlayerToolConstants.VisualAssetBundleName);
        if (File.Exists(assetFolderPath))
        {
            return assetFolderPath;
        }

        return null;
    }

    private static string ResolvePluginFolder(string pluginLocation)
    {
        if (string.IsNullOrWhiteSpace(pluginLocation))
        {
            return AppDomain.CurrentDomain.BaseDirectory;
        }

        return Path.GetDirectoryName(pluginLocation) ?? AppDomain.CurrentDomain.BaseDirectory;
    }

    private static string GetTransformPath(Transform transform)
    {
        var path = transform.name;
        var current = transform.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }
}
