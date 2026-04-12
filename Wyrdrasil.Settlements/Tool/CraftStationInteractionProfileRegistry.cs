using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wyrdrasil.Settlements.Tool;

public static class CraftStationInteractionProfileRegistry
{
    public const string DefaultProfileId = "craftstation.default";
    public const string WorkbenchProfileId = "craftstation.workbench";

    private static readonly Dictionary<string, CraftStationInteractionProfile> ProfilesById = new(StringComparer.OrdinalIgnoreCase)
    {
        [DefaultProfileId] = new CraftStationInteractionProfile(
            DefaultProfileId,
            new Vector3(0f, 0.05f, 1.10f),
            Vector3.back,
            0.85f,
            0.45f,
            0.85f,
            1.35f),

        [WorkbenchProfileId] = new CraftStationInteractionProfile(
            WorkbenchProfileId,
            new Vector3(0.32f, 0.23f, 1.46f),
            new Vector3(0.13f, 0f, 0.99f),
            0.85f,
            0.45f,
            0.85f,
            1.35f)
    };

    private static readonly Dictionary<string, string> ProfileIdsByPrefabName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["piece_workbench(Clone)"] = WorkbenchProfileId,
        ["piece_workbench"] = WorkbenchProfileId
    };

    public static CraftStationInteractionProfile GetDefaultProfile() => ProfilesById[DefaultProfileId];

    public static bool TryGetProfileById(string? profileId, out CraftStationInteractionProfile profile)
    {
        if (!string.IsNullOrWhiteSpace(profileId) && ProfilesById.TryGetValue(profileId, out profile!))
        {
            return true;
        }

        profile = null!;
        return false;
    }

    public static bool TryGetProfileForPrefab(string? prefabName, out CraftStationInteractionProfile profile)
    {
        if (!string.IsNullOrWhiteSpace(prefabName) &&
            ProfileIdsByPrefabName.TryGetValue(prefabName, out var profileId) &&
            ProfilesById.TryGetValue(profileId, out profile!))
        {
            return true;
        }

        profile = null!;
        return false;
    }
}
