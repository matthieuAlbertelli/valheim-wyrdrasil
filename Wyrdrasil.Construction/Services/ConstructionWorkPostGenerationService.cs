using System.Collections.Generic;
using UnityEngine;
using Wyrdrasil.Construction.Models;

namespace Wyrdrasil.Construction.Services;

public sealed class ConstructionWorkPostGenerationService
{
    public List<ConstructionWorkPostData> GenerateWorkPosts(
        int projectId,
        StructureBlueprintData blueprint,
        Vector3 originPosition,
        Quaternion originRotation,
        System.Func<int> allocateWorkPostId)
    {
        return new List<ConstructionWorkPostData>
        {
            new ConstructionWorkPostData
            {
                Id = allocateWorkPostId(),
                ProjectId = projectId,
                CraftStationId = 0,
                WorldPosition = originPosition,
                WorldRotation = originRotation,
                AssignedResidentId = null
            }
        };
    }
}
