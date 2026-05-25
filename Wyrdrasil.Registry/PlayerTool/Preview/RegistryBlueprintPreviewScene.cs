using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Wyrdrasil.Registry.PlayerTool.Preview;

public sealed class RegistryBlueprintPreviewScene : IDisposable
{
    public GameObject Root { get; }
    public IReadOnlyList<Renderer> Renderers { get; }
    public Bounds Bounds { get; }
    public int VisualPieceCount { get; }

    public bool HasRenderableContent => Renderers.Count > 0 && VisualPieceCount > 0;

    public RegistryBlueprintPreviewScene(
        GameObject root,
        IReadOnlyList<Renderer> renderers,
        Bounds bounds,
        int visualPieceCount)
    {
        Root = root;
        Renderers = renderers;
        Bounds = bounds;
        VisualPieceCount = visualPieceCount;
    }

    public void Dispose()
    {
        if (Root != null)
        {
            Object.Destroy(Root);
        }
    }
}
