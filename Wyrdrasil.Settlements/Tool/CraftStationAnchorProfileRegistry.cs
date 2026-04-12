using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wyrdrasil.Settlements.Tool;

public static class CraftStationAnchorProfileRegistry
{
    private static readonly Dictionary<string, CraftStationAnchorProfile> ProfilesByPrefabName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["piece_workbench(Clone)"] = new CraftStationAnchorProfile(
            "piece_workbench(Clone)",
            new Vector3(0.22f, 0.08f, 1.42f),
            new Vector3(0.32f, 0.23f, 1.46f),
            new Vector3(-0.13f, 0f, -0.99f)),
        ["piece_workbench"] = new CraftStationAnchorProfile(
            "piece_workbench",
            new Vector3(0.22f, 0.08f, 1.42f),
            new Vector3(0.32f, 0.23f, 1.46f),
            new Vector3(-0.13f, 0f, -0.99f))
    };

    public static bool TryGetProfile(string? prefabName, out CraftStationAnchorProfile profile)
    {
        if (!string.IsNullOrWhiteSpace(prefabName) && ProfilesByPrefabName.TryGetValue(prefabName, out profile!))
        {
            return true;
        }

        profile = null!;
        return false;
    }
}
