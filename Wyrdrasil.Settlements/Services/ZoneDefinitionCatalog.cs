using System;
using System.Collections.Generic;
using Wyrdrasil.Settlements.Tool;

namespace Wyrdrasil.Settlements.Services;

public sealed class ZoneDefinitionCatalog
{
    private readonly Dictionary<ZoneType, ZoneTypeDefinition> _definitionsByType;

    public ZoneDefinitionCatalog()
    {
        _definitionsByType = new Dictionary<ZoneType, ZoneTypeDefinition>
        {
            [ZoneType.Tavern] = new ZoneTypeDefinition(
                ZoneType.Tavern,
                "Tavern",
                ZoneCapability.SocialGathering | ZoneCapability.PublicSeating | ZoneCapability.HospitalityService,
                new[] { ZoneDesignationKind.PublicSeat, ZoneDesignationKind.ReservedSeat, ZoneDesignationKind.InnkeeperSlot },
                new[]
                {
                    new DefaultInterpretationRule(ZoneDesignationKind.PublicSeat, ZoneInterpretationKind.PublicSocialSeating),
                    new DefaultInterpretationRule(ZoneDesignationKind.InnkeeperSlot, ZoneInterpretationKind.HospitalityStaffWork)
                },
                new ZoneRequirementProfile(minimumPublicSeatCount: 1),
                new ZoneRequirementProfile(minimumPublicSeatCount: 1, minimumInnkeeperSlotCount: 1, minimumAssignedInnkeeperCount: 1),
                new ZoneRequirementProfile(minimumPublicSeatCount: 1, minimumInnkeeperSlotCount: 1, minimumAssignedInnkeeperCount: 1)),

            [ZoneType.Bedroom] = new ZoneTypeDefinition(
                ZoneType.Bedroom,
                "Bedroom",
                ZoneCapability.PrivateRest,
                new[] { ZoneDesignationKind.Bed, ZoneDesignationKind.ReservedSeat },
                new[]
                {
                    new DefaultInterpretationRule(ZoneDesignationKind.Bed, ZoneInterpretationKind.PrivateSleeping)
                },
                new ZoneRequirementProfile(minimumBedCount: 1),
                new ZoneRequirementProfile(),
                new ZoneRequirementProfile(minimumBedCount: 1)),

            [ZoneType.Forge] = new ZoneTypeDefinition(
                ZoneType.Forge,
                "Forge",
                ZoneCapability.CraftWork,
                new[] { ZoneDesignationKind.CraftStation },
                new[]
                {
                    new DefaultInterpretationRule(ZoneDesignationKind.CraftStation, ZoneInterpretationKind.CraftWorkstation)
                },
                new ZoneRequirementProfile(minimumCraftStationCount: 1),
                new ZoneRequirementProfile(),
                new ZoneRequirementProfile(minimumCraftStationCount: 1)),

            [ZoneType.Barracks] = new ZoneTypeDefinition(
                ZoneType.Barracks,
                "Barracks",
                ZoneCapability.PrivateRest | ZoneCapability.GuardAssembly,
                new[] { ZoneDesignationKind.Bed },
                new[]
                {
                    new DefaultInterpretationRule(ZoneDesignationKind.Bed, ZoneInterpretationKind.PrivateSleeping)
                },
                new ZoneRequirementProfile(minimumBedCount: 1),
                new ZoneRequirementProfile(),
                new ZoneRequirementProfile(minimumBedCount: 1)),

            [ZoneType.Courtyard] = new ZoneTypeDefinition(
                ZoneType.Courtyard,
                "Courtyard",
                ZoneCapability.SocialGathering | ZoneCapability.PublicSeating,
                new[] { ZoneDesignationKind.PublicSeat, ZoneDesignationKind.ReservedSeat },
                new[]
                {
                    new DefaultInterpretationRule(ZoneDesignationKind.PublicSeat, ZoneInterpretationKind.PublicSocialSeating)
                },
                new ZoneRequirementProfile(minimumPublicSeatCount: 1),
                new ZoneRequirementProfile(),
                new ZoneRequirementProfile(minimumPublicSeatCount: 1)),

            [ZoneType.Kitchen] = new ZoneTypeDefinition(
                ZoneType.Kitchen,
                "Kitchen",
                ZoneCapability.FoodPreparation | ZoneCapability.PublicSeating,
                new[] { ZoneDesignationKind.PublicSeat },
                new[]
                {
                    new DefaultInterpretationRule(ZoneDesignationKind.PublicSeat, ZoneInterpretationKind.PublicSocialSeating)
                },
                new ZoneRequirementProfile(minimumPublicSeatCount: 1),
                new ZoneRequirementProfile(),
                new ZoneRequirementProfile(minimumPublicSeatCount: 1))
        };
    }

    public bool TryGetDefinition(ZoneType zoneType, out ZoneTypeDefinition definition)
    {
        return _definitionsByType.TryGetValue(zoneType, out definition!);
    }

    public ZoneTypeDefinition GetDefinition(ZoneType zoneType)
    {
        if (TryGetDefinition(zoneType, out var definition))
        {
            return definition;
        }

        throw new InvalidOperationException($"No functional definition is registered for zone type '{zoneType}'.");
    }
}
