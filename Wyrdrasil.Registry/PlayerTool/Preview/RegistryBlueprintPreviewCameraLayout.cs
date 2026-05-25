using System;
using UnityEngine;

namespace Wyrdrasil.Registry.PlayerTool.Preview;

public readonly struct RegistryBlueprintPreviewCameraLayout
{
    public Vector3 Target { get; }
    public Vector3 Position { get; }
    public Quaternion Rotation { get; }
    public float OrthographicSize { get; }
    public float NearClipPlane { get; }
    public float FarClipPlane { get; }

    public RegistryBlueprintPreviewCameraLayout(
        Vector3 target,
        Vector3 position,
        Quaternion rotation,
        float orthographicSize,
        float nearClipPlane,
        float farClipPlane)
    {
        Target = target;
        Position = position;
        Rotation = rotation;
        OrthographicSize = orthographicSize;
        NearClipPlane = nearClipPlane;
        FarClipPlane = farClipPlane;
    }
}

public static class RegistryBlueprintPreviewCameraFraming
{
    private const float Padding = 1.08f;
    private const float MinimumOrthographicSize = 1.15f;
    private const float MinimumDistance = 10f;
    private const float StandardYawDegrees = 225f;
    private const float StandardPitchDegrees = 32f;

    public static RegistryBlueprintPreviewCameraLayout Compute(Bounds bounds, Vector3 viewDirection)
    {
        if (viewDirection.sqrMagnitude <= 0.001f)
        {
            viewDirection = Quaternion.Euler(StandardPitchDegrees, StandardYawDegrees, 0f) * Vector3.forward;
        }

        viewDirection.Normalize();

        var target = bounds.center;
        var rotation = Quaternion.LookRotation(viewDirection, Vector3.up);
        var right = rotation * Vector3.right;
        var up = rotation * Vector3.up;

        var projectedHalfWidth = 0f;
        var projectedHalfHeight = 0f;
        var corners = GetBoundsCorners(bounds);
        for (var i = 0; i < corners.Length; i++)
        {
            var relative = corners[i] - target;
            projectedHalfWidth = Math.Max(projectedHalfWidth, Mathf.Abs(Vector3.Dot(relative, right)));
            projectedHalfHeight = Math.Max(projectedHalfHeight, Mathf.Abs(Vector3.Dot(relative, up)));
        }

        var orthographicSize = Math.Max(
            MinimumOrthographicSize,
            Math.Max(projectedHalfHeight, projectedHalfWidth) * Padding);

        var radius = Math.Max(bounds.extents.magnitude, 1f);
        var distance = Math.Max(MinimumDistance, radius * 3.35f);
        var position = target - (viewDirection * distance);
        var farClip = Math.Max(100f, distance + (radius * 4f) + 10f);

        return new RegistryBlueprintPreviewCameraLayout(
            target,
            position,
            rotation,
            orthographicSize,
            0.01f,
            farClip);
    }

    private static Vector3[] GetBoundsCorners(Bounds bounds)
    {
        var min = bounds.min;
        var max = bounds.max;
        return new[]
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(max.x, max.y, max.z)
        };
    }
}
